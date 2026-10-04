using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeachingService.Contracts.Enums;
using TeachingService.Contracts.Requests;
using TeachingService.Contracts.Responses;
using TeachingService.Controllers;
using TeachingService.Data;
using TeachingService.Enums;
using TeachingService.Models;
using TeachingService.Services;

namespace TeachingService.Tests;

public class StudyGroupServiceTests
{
    [Fact]
    public async Task AdminCreateGroup_UsesSelectedTeacher()
    {
        await using var dbContext = CreateDbContext();
        var controller = new StudyGroupsController(new StudyGroupService(dbContext))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, "admin-1"),
                        new Claim(ClaimTypes.Role, "Admin")
                    ],
                    "TestAuthentication"))
                }
            }
        };

        var result = await controller.CreateGroup(
            new CreateStudyGroupRequest
            {
                Name = "Admin group",
                TeacherUserId = "teacher-2",
                StudentUserIds = ["student-1"]
            },
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var group = Assert.IsType<StudyGroupResponse>(created.Value);
        Assert.Equal("teacher-2", group.TeacherUserId);
        Assert.Equal(["student-1"], group.StudentUserIds);
    }

    [Fact]
    public async Task CreateAssignment_PinsPublishedRevisionAndLimitsAccessToGroupMembers()
    {
        await using var dbContext = CreateDbContext();
        var test = CreateVersionedTest("teacher-1");
        dbContext.Tests.Add(test);
        await dbContext.SaveChangesAsync();
        var service = new StudyGroupService(dbContext);
        var group = await CreateGroupAsync(service, "teacher-1", ["student-1"]);
        var availableFrom = DateTimeOffset.UtcNow.AddMinutes(-5);
        var deadline = DateTimeOffset.UtcNow.AddDays(2);
        var assignableTests = await service.GetAssignableTestsAsync(
            group.Id,
            "teacher-1",
            CancellationToken.None);

        var result = await service.CreateAssignmentAsync(
            group.Id,
            new CreateTestAssignmentRequest
            {
                TestId = test.Id,
                TestRevision = 1,
                AvailableFrom = availableFrom,
                Deadline = deadline
            },
            "teacher-1",
            CancellationToken.None);

        Assert.NotNull(assignableTests);
        Assert.Equal([1, 2], assignableTests
            .Select(item => item.VersionNumber)
            .OrderBy(item => item));
        Assert.Equal(GroupOperationStatus.Success, result.Status);
        Assert.NotNull(result.Value);
        Assert.Equal(1, result.Value.Test.VersionNumber);
        Assert.Equal("First revision", result.Value.Test.Title);
        Assert.Equal(availableFrom, result.Value.AvailableFrom);
        Assert.Equal(deadline, result.Value.Deadline);

        var memberAssignments = await service.GetStudentAssignmentsAsync(
            "student-1",
            CancellationToken.None);
        var outsiderAssignments = await service.GetStudentAssignmentsAsync(
            "student-2",
            CancellationToken.None);
        var loadedGroup = await service.GetGroupAsync(
            group.Id,
            "teacher-1",
            includeHidden: true,
            CancellationToken.None);

        Assert.Equal(result.Value.Id, Assert.Single(memberAssignments).Id);
        Assert.Empty(outsiderAssignments);
        Assert.NotNull(loadedGroup);
        Assert.Equal(result.Value.Id, Assert.Single(loadedGroup.Assignments).Id);
        Assert.Null(await service.GetStudentAssignmentAsync(
            result.Value.Id,
            "student-2",
            CancellationToken.None));
    }

    [Fact]
    public async Task CreateAssignment_AllowsSamePublishedRevisionToBeAssignedAgain()
    {
        await using var dbContext = CreateDbContext();
        var test = CreateVersionedTest("teacher-1");
        dbContext.Tests.Add(test);
        await dbContext.SaveChangesAsync();
        var service = new StudyGroupService(dbContext);
        var group = await CreateGroupAsync(service, "teacher-1", ["student-1"]);

        var first = await CreateAssignmentAsync(
            service,
            group.Id,
            test.Id,
            DateTimeOffset.UtcNow.AddDays(1));
        var second = await CreateAssignmentAsync(
            service,
            group.Id,
            test.Id,
            DateTimeOffset.UtcNow.AddDays(2));

        Assert.Equal(GroupOperationStatus.Success, first.Status);
        Assert.Equal(GroupOperationStatus.Success, second.Status);
        Assert.NotEqual(first.Value!.Id, second.Value!.Id);
        var assignments = await service.GetStudentAssignmentsAsync(
            "student-1",
            CancellationToken.None);
        Assert.Equal(2, assignments.Count);
    }

    [Fact]
    public async Task TeacherScope_DoesNotExposeAnotherTeachersGroup()
    {
        await using var dbContext = CreateDbContext();
        var service = new StudyGroupService(dbContext);
        var group = await CreateGroupAsync(service, "teacher-1", ["student-1"]);

        var ownerResult = await service.GetGroupAsync(
            group.Id,
            "teacher-1",
            includeHidden: true,
            CancellationToken.None);
        var foreignResult = await service.GetGroupAsync(
            group.Id,
            "teacher-2",
            includeHidden: true,
            CancellationToken.None);
        var adminResult = await service.GetGroupAsync(
            group.Id,
            teacherUserId: null,
            includeHidden: true,
            CancellationToken.None);

        Assert.NotNull(ownerResult);
        Assert.Null(foreignResult);
        Assert.NotNull(adminResult);
    }

    [Fact]
    public async Task UpdateGroup_ReplacesMembershipAndRejectsStaleRevision()
    {
        await using var dbContext = CreateDbContext();
        var service = new StudyGroupService(dbContext);
        var group = await CreateGroupAsync(service, "teacher-1", ["student-1"]);

        var updated = await service.UpdateGroupAsync(
            group.Id,
            new UpdateStudyGroupRequest
            {
                Name = "Updated group",
                StudentUserIds = ["student-2"],
                Revision = group.Revision
            },
            "teacher-1",
            CancellationToken.None);
        var stale = await service.UpdateGroupAsync(
            group.Id,
            new UpdateStudyGroupRequest
            {
                Name = "Stale name",
                StudentUserIds = ["student-3"],
                Revision = group.Revision
            },
            "teacher-1",
            CancellationToken.None);

        Assert.Equal(GroupOperationStatus.Success, updated.Status);
        Assert.Equal("Updated group", updated.Value!.Name);
        Assert.Equal(["student-2"], updated.Value.StudentUserIds);
        Assert.Equal(GroupOperationStatus.Conflict, stale.Status);
    }

    private static TeachingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TeachingDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var dbContext = new TeachingDbContext(options);
        dbContext.Database.OpenConnection();
        dbContext.Database.EnsureCreated();
        return dbContext;
    }

    private static async Task<StudyGroupResponse> CreateGroupAsync(
        StudyGroupService service,
        string teacherUserId,
        IList<string> studentUserIds)
    {
        var result = await service.CreateGroupAsync(
            new CreateStudyGroupRequest
            {
                Name = "Group 1",
                StudentUserIds = studentUserIds
            },
            teacherUserId,
            CancellationToken.None);

        Assert.Equal(GroupOperationStatus.Success, result.Status);
        return Assert.IsType<StudyGroupResponse>(result.Value);
    }

    private static Task<Result<GroupOperationStatus, TestAssignmentResponse>>
        CreateAssignmentAsync(
            StudyGroupService service,
            Guid groupId,
            Guid testId,
            DateTimeOffset deadline)
    {
        return service.CreateAssignmentAsync(
            groupId,
            new CreateTestAssignmentRequest
            {
                TestId = testId,
                TestRevision = 1,
                AvailableFrom = DateTimeOffset.UtcNow.AddMinutes(-1),
                Deadline = deadline
            },
            "teacher-1",
            CancellationToken.None);
    }

    private static CourseTest CreateVersionedTest(string teacherUserId)
    {
        var testId = Guid.NewGuid();
        var firstVersionId = Guid.NewGuid();
        var secondVersionId = Guid.NewGuid();
        return new CourseTest
        {
            Id = testId,
            TeacherUserId = teacherUserId,
            NextVersionNumber = 3,
            Versions =
            [
                new CourseTestVersion
                {
                    Id = firstVersionId,
                    CourseTestId = testId,
                    VersionNumber = 1,
                    Status = CourseTestStatus.Superseded,
                    Title = "First revision",
                    Subject = "Subject",
                    TimeLimitMinutes = 30,
                    PublishedAt = DateTimeOffset.UtcNow.AddDays(-2),
                    Tasks =
                    [
                        new TestTask
                        {
                            Id = Guid.NewGuid(),
                            CourseTestVersionId = firstVersionId,
                            Type = TestTaskType.FreeText,
                            CheckMode = TestTaskCheckMode.Manual,
                            Title = "First task",
                            Prompt = "Answer",
                            MaxPoints = 10
                        }
                    ]
                },
                new CourseTestVersion
                {
                    Id = secondVersionId,
                    CourseTestId = testId,
                    VersionNumber = 2,
                    Status = CourseTestStatus.Published,
                    Title = "Second revision",
                    Subject = "Subject",
                    TimeLimitMinutes = 45,
                    PublishedAt = DateTimeOffset.UtcNow.AddDays(-1),
                    Tasks =
                    [
                        new TestTask
                        {
                            Id = Guid.NewGuid(),
                            CourseTestVersionId = secondVersionId,
                            Type = TestTaskType.FreeText,
                            CheckMode = TestTaskCheckMode.Manual,
                            Title = "Second task",
                            Prompt = "Answer",
                            MaxPoints = 20
                        }
                    ]
                }
            ]
        };
    }
}

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
        var controller = CreateController(new StudyGroupService(dbContext), "admin-1", "Admin");

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
    public async Task AdminCreateAssignment_IsForbiddenAndDoesNotPersistAssignment()
    {
        await using var dbContext = CreateDbContext();
        var test = CreateVersionedTest("teacher-1");
        dbContext.Tests.Add(test);
        await dbContext.SaveChangesAsync();
        var service = new StudyGroupService(dbContext);
        var group = await CreateGroupAsync(service, "teacher-1", ["student-1"]);
        var controller = CreateController(service, "admin-1", "Admin");

        var result = await controller.CreateAssignment(
            group.Id,
            CreateAssignmentRequest(test.Id),
            CancellationToken.None);

        Assert.IsType<ForbidResult>(result.Result);
        Assert.Empty(await dbContext.TestAssignments.ToListAsync());
        Assert.Empty(await service.GetStudentAssignmentsAsync("student-1", CancellationToken.None));
    }

    [Fact]
    public async Task AdminGetAssignableTests_IsForbidden()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Tests.Add(CreateVersionedTest("teacher-1"));
        await dbContext.SaveChangesAsync();
        var service = new StudyGroupService(dbContext);
        var group = await CreateGroupAsync(service, "teacher-1", ["student-1"]);
        var controller = CreateController(service, "admin-1", "Admin");

        var result = await controller.GetAssignableTests(group.Id, CancellationToken.None);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task TeacherWithoutUserId_CannotGetAssignableTestsOrCreateAssignment()
    {
        await using var dbContext = CreateDbContext();
        var test = CreateVersionedTest("teacher-1");
        dbContext.Tests.Add(test);
        await dbContext.SaveChangesAsync();
        var service = new StudyGroupService(dbContext);
        var group = await CreateGroupAsync(service, "teacher-1", ["student-1"]);
        var controller = CreateController(service, userId: null, "Teacher");

        var assignableTests = await controller.GetAssignableTests(group.Id, CancellationToken.None);
        var assignment = await controller.CreateAssignment(
            group.Id,
            CreateAssignmentRequest(test.Id),
            CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(assignableTests.Result);
        Assert.IsType<UnauthorizedResult>(assignment.Result);
        Assert.Empty(await dbContext.TestAssignments.ToListAsync());
    }

    [Theory]
    [InlineData("Teacher")]
    [InlineData("Teacher", "Admin")]
    public async Task TeacherCannotAssignTestsToAnotherTeachersGroup(params string[] roles)
    {
        await using var dbContext = CreateDbContext();
        var test = CreateVersionedTest("teacher-1");
        dbContext.Tests.Add(test);
        await dbContext.SaveChangesAsync();
        var service = new StudyGroupService(dbContext);
        var group = await CreateGroupAsync(service, "teacher-1", ["student-1"]);
        var controller = CreateController(service, "teacher-2", roles);

        var assignableTests = await controller.GetAssignableTests(group.Id, CancellationToken.None);
        var assignment = await controller.CreateAssignment(
            group.Id,
            CreateAssignmentRequest(test.Id),
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(assignableTests.Result);
        Assert.IsType<NotFoundResult>(assignment.Result);
        Assert.Empty(await dbContext.TestAssignments.ToListAsync());
    }

    [Fact]
    public async Task AdminUpdateGroup_ChangesTeacherAndMembershipAndTransfersAssignmentRights()
    {
        await using var dbContext = CreateDbContext();
        var originalTest = CreateVersionedTest("teacher-1");
        var replacementTest = CreateVersionedTest("teacher-2");
        dbContext.Tests.AddRange(originalTest, replacementTest);
        await dbContext.SaveChangesAsync();
        var service = new StudyGroupService(dbContext);
        var group = await CreateGroupAsync(service, "teacher-1", ["student-1"]);
        var adminController = CreateController(service, "admin-1", "Admin");

        var update = await adminController.UpdateGroup(
            group.Id,
            new UpdateStudyGroupRequest
            {
                Name = "Transferred group",
                TeacherUserId = "teacher-2",
                StudentUserIds = ["student-2", "student-3"],
                Revision = group.Revision
            },
            CancellationToken.None);

        var updatedGroup = Assert.IsType<StudyGroupResponse>(Assert.IsType<OkObjectResult>(update.Result).Value);
        Assert.Equal("Transferred group", updatedGroup.Name);
        Assert.Equal("teacher-2", updatedGroup.TeacherUserId);
        Assert.Equal(["student-2", "student-3"], updatedGroup.StudentUserIds);
        Assert.Equal(group.Revision + 1, updatedGroup.Revision);

        var previousTeacher = CreateController(service, "teacher-1", "Teacher");
        var previousAssignableTests = await previousTeacher.GetAssignableTests(group.Id, CancellationToken.None);
        var previousAssignment = await previousTeacher.CreateAssignment(
            group.Id,
            CreateAssignmentRequest(originalTest.Id),
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(previousAssignableTests.Result);
        Assert.IsType<NotFoundResult>(previousAssignment.Result);
        Assert.Empty(await dbContext.TestAssignments.ToListAsync());

        var currentTeacher = CreateController(service, "teacher-2", "Teacher");
        var assignableTests = await service.GetAssignableTestsAsync(
            group.Id,
            "teacher-2",
            CancellationToken.None);
        var assignment = await currentTeacher.CreateAssignment(
            group.Id,
            CreateAssignmentRequest(replacementTest.Id),
            CancellationToken.None);

        Assert.NotNull(assignableTests);
        Assert.Equal([1, 2], assignableTests.Select(test => test.VersionNumber).OrderBy(version => version));
        Assert.All(assignableTests, test => Assert.Equal(replacementTest.Id.ToString(), test.Id));
        var assignedTest = Assert.IsType<TestAssignmentResponse>(
            Assert.IsType<CreatedAtActionResult>(assignment.Result).Value);
        Assert.Equal("teacher-2", assignedTest.TeacherUserId);
        Assert.Equal(replacementTest.Id.ToString(), assignedTest.Test.Id);
        Assert.Equal(assignedTest.Id, Assert.Single(await dbContext.TestAssignments.ToListAsync()).Id);
        Assert.Empty(await service.GetStudentAssignmentsAsync("student-1", CancellationToken.None));
        Assert.Equal(assignedTest.Id, Assert.Single(await service.GetStudentAssignmentsAsync(
            "student-2", CancellationToken.None)).Id);
    }

    [Fact]
    public async Task AdminTransferGroup_KeepsExistingAssignmentsWithOriginalTeacher()
    {
        await using var dbContext = CreateDbContext();
        var originalTest = CreateVersionedTest("teacher-1");
        var replacementTest = CreateVersionedTest("teacher-2");
        dbContext.Tests.AddRange(originalTest, replacementTest);
        await dbContext.SaveChangesAsync();
        var service = new StudyGroupService(dbContext);
        var group = await CreateGroupAsync(service, "teacher-1", ["student-1"]);
        var deadline = DateTimeOffset.UtcNow.AddDays(2);
        var originalAssignmentResult = await CreateAssignmentAsync(service, group.Id, originalTest.Id, deadline);
        Assert.Equal(GroupOperationStatus.Success, originalAssignmentResult.Status);
        var originalAssignment = Assert.IsType<TestAssignmentResponse>(originalAssignmentResult.Value);

        var transfer = await service.UpdateGroupAsync(
            group.Id,
            new UpdateStudyGroupRequest
            {
                Name = group.Name,
                TeacherUserId = "teacher-2",
                StudentUserIds = ["student-1"],
                Revision = group.Revision
            },
            teacherUserId: null,
            CancellationToken.None);

        Assert.Equal(GroupOperationStatus.Success, transfer.Status);
        var transferredGroup = Assert.IsType<StudyGroupResponse>(transfer.Value);
        Assert.Equal("teacher-2", transferredGroup.TeacherUserId);
        Assert.Equal(["student-1"], transferredGroup.StudentUserIds);

        var replacementAssignmentResult = await service.CreateAssignmentAsync(
            group.Id,
            CreateAssignmentRequest(replacementTest.Id),
            "teacher-2",
            CancellationToken.None);

        Assert.Equal(GroupOperationStatus.Success, replacementAssignmentResult.Status);
        var replacementAssignment = Assert.IsType<TestAssignmentResponse>(replacementAssignmentResult.Value);
        Assert.Equal("teacher-2", replacementAssignment.TeacherUserId);
        Assert.Equal(replacementTest.Id.ToString(), replacementAssignment.Test.Id);
        Assert.NotEqual(originalAssignment.Id, replacementAssignment.Id);

        var loadedGroup = await service.GetGroupAsync(
            group.Id,
            "teacher-2",
            includeHidden: true,
            CancellationToken.None);
        var studentAssignments = await service.GetStudentAssignmentsAsync("student-1", CancellationToken.None);
        var loadedAssignment = await service.GetAssignmentAsync(
            originalAssignment.Id,
            includeHidden: true,
            CancellationToken.None);

        Assert.NotNull(loadedGroup);
        Assert.Equal("teacher-2", loadedGroup.TeacherUserId);
        Assert.Equal(2, loadedGroup.Assignments.Count);
        Assert.Equal(2, studentAssignments.Count);
        IList<TestAssignmentResponse> existingAssignments =
        [
            Assert.Single(loadedGroup.Assignments, assignment => assignment.Id == originalAssignment.Id),
            Assert.Single(studentAssignments, assignment => assignment.Id == originalAssignment.Id),
            Assert.IsType<TestAssignmentResponse>(loadedAssignment)
        ];
        Assert.All(existingAssignments, assignment =>
        {
            Assert.Equal(originalAssignment.Id, assignment.Id);
            Assert.Equal(group.Id, assignment.GroupId);
            Assert.Equal("teacher-1", assignment.TeacherUserId);
            Assert.Equal(originalTest.Id.ToString(), assignment.Test.Id);
            Assert.Equal(1, assignment.Test.VersionNumber);
            Assert.Equal("First revision", assignment.Test.Title);
            Assert.Equal(deadline, assignment.Deadline);
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task AdminUpdateGroup_RequiresTeacherAndDoesNotChangeGroup(string? teacherUserId)
    {
        await using var dbContext = CreateDbContext();
        var service = new StudyGroupService(dbContext);
        var group = await CreateGroupAsync(service, "teacher-1", ["student-1"]);
        var controller = CreateController(service, "admin-1", "Admin");

        var result = await controller.UpdateGroup(
            group.Id,
            new UpdateStudyGroupRequest
            {
                Name = "Changed group",
                TeacherUserId = teacherUserId,
                StudentUserIds = ["student-2"],
                Revision = group.Revision
            },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        var storedGroup = await service.GetGroupAsync(
            group.Id,
            teacherUserId: null,
            includeHidden: true,
            CancellationToken.None);
        Assert.NotNull(storedGroup);
        Assert.Equal(group.Name, storedGroup.Name);
        Assert.Equal("teacher-1", storedGroup.TeacherUserId);
        Assert.Equal(["student-1"], storedGroup.StudentUserIds);
        Assert.Equal(group.Revision, storedGroup.Revision);
    }

    [Fact]
    public async Task TeacherUpdateGroup_DoesNotChangeTeacher()
    {
        await using var dbContext = CreateDbContext();
        var service = new StudyGroupService(dbContext);
        var group = await CreateGroupAsync(service, "teacher-1", ["student-1"]);
        var controller = CreateController(service, "teacher-1", "Teacher");

        var result = await controller.UpdateGroup(
            group.Id,
            new UpdateStudyGroupRequest
            {
                Name = "Updated group",
                TeacherUserId = "teacher-2",
                StudentUserIds = ["student-2"],
                Revision = group.Revision
            },
            CancellationToken.None);

        var updatedGroup = Assert.IsType<StudyGroupResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal("Updated group", updatedGroup.Name);
        Assert.Equal("teacher-1", updatedGroup.TeacherUserId);
        Assert.Equal(["student-2"], updatedGroup.StudentUserIds);
        var storedGroup = await dbContext.StudyGroups.SingleAsync(group => group.Id == updatedGroup.Id);
        Assert.Equal("teacher-1", storedGroup.TeacherUserId);
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

    private static StudyGroupsController CreateController(
        StudyGroupService service,
        string? userId,
        params string[] roles)
    {
        var claims = roles.Select(role => new Claim(ClaimTypes.Role, role)).ToList();
        if (userId is not null)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
        }

        return new StudyGroupsController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuthentication"))
                }
            }
        };
    }

    private static CreateTestAssignmentRequest CreateAssignmentRequest(Guid testId)
    {
        return new CreateTestAssignmentRequest
        {
            TestId = testId,
            TestRevision = 1,
            AvailableFrom = DateTimeOffset.UtcNow.AddMinutes(-1),
            Deadline = DateTimeOffset.UtcNow.AddDays(1)
        };
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

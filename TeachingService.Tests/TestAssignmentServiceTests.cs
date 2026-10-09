using System.Net;
using System.Security.Claims;
using System.Text.Json;
using GroupService.Contracts.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeachingService.Contracts.Enums;
using TeachingService.Contracts.Models;
using TeachingService.Contracts.Requests;
using TeachingService.Contracts.Responses;
using TeachingService.Controllers;
using TeachingService.Data;
using TeachingService.Enums;
using TeachingService.Interfaces;
using TeachingService.Helpers;
using TeachingService.Models;
using TeachingService.Services;

namespace TeachingService.Tests;

public class TestAssignmentServiceTests
{
    [Fact]
    public async Task AdminCannotCreateAssignmentsOrLoadAssignableTests()
    {
        await using var dbContext = CreateDbContext();
        var test = CreateVersionedTest("teacher-1");
        var groups = new FakeGroupServiceClient(CreateGroup());
        var service = await CreateServiceAsync(dbContext, groups, test);
        var controller = CreateController(service, "admin-1", "Admin");

        var assignment = await controller.CreateAssignment(groups.Group.Id, CreateRequest(test.Id), CancellationToken.None);
        var catalog = await controller.GetAssignableTests(groups.Group.Id, CancellationToken.None);

        Assert.IsType<ForbidResult>(assignment.Result);
        Assert.IsType<ForbidResult>(catalog.Result);
        Assert.Empty(await dbContext.TestAssignments.ToListAsync());
        Assert.Equal(0, groups.GroupRequests);
    }

    [Fact]
    public async Task TeacherWithoutUserIdCannotCreateAssignmentsOrLoadAssignableTests()
    {
        await using var dbContext = CreateDbContext();
        var test = CreateVersionedTest("teacher-1");
        var groups = new FakeGroupServiceClient(CreateGroup());
        var service = await CreateServiceAsync(dbContext, groups, test);
        var controller = CreateController(service, userId: null, "Teacher");

        var assignment = await controller.CreateAssignment(groups.Group.Id, CreateRequest(test.Id), CancellationToken.None);
        var catalog = await controller.GetAssignableTests(groups.Group.Id, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(assignment.Result);
        Assert.IsType<UnauthorizedResult>(catalog.Result);
        Assert.Empty(await dbContext.TestAssignments.ToListAsync());
        Assert.Equal(0, groups.GroupRequests);
    }

    [Fact]
    public async Task TeacherCannotAccessOrAssignAnotherTeachersGroup()
    {
        await using var dbContext = CreateDbContext();
        var test = CreateVersionedTest("teacher-2");
        var groups = new FakeGroupServiceClient(CreateGroup());
        var service = await CreateServiceAsync(dbContext, groups, test);
        var controller = CreateController(service, "teacher-2", "Teacher");

        var assignment = await controller.CreateAssignment(groups.Group.Id, CreateRequest(test.Id), CancellationToken.None);
        var catalog = await controller.GetAssignableTests(groups.Group.Id, CancellationToken.None);
        var groupAssignments = await controller.GetAssignments(groups.Group.Id, CancellationToken.None);

        Assert.IsType<NotFoundResult>(assignment.Result);
        Assert.IsType<NotFoundResult>(catalog.Result);
        Assert.IsType<NotFoundResult>(groupAssignments.Result);
        Assert.Empty(await dbContext.TestAssignments.ToListAsync());
    }

    [Fact]
    public async Task TeacherCannotAssignAnotherTeachersTestToOwnGroup()
    {
        await using var dbContext = CreateDbContext();
        var ownTest = CreateVersionedTest("teacher-1");
        var foreignTest = CreateVersionedTest("teacher-2");
        var groups = new FakeGroupServiceClient(CreateGroup());
        var service = await CreateServiceAsync(dbContext, groups, ownTest, foreignTest);

        var catalog = await service.GetAssignableTestsAsync(groups.Group.Id, "teacher-1", CancellationToken.None);
        var assignment = await service.CreateAssignmentAsync(
            groups.Group.Id, CreateRequest(foreignTest.Id), "teacher-1", CancellationToken.None);

        Assert.NotNull(catalog);
        Assert.Equal(2, catalog.Count);
        Assert.All(catalog, test => Assert.Equal(ownTest.Id.ToString(), test.Id));
        Assert.Equal(AssignmentOperationStatus.ValidationFailed, assignment.Status);
        Assert.Empty(await dbContext.TestAssignments.ToListAsync());
    }

    [Fact]
    public async Task TeacherCreatesAssignmentPinnedToSupersededPublishedVersion()
    {
        await using var dbContext = CreateDbContext();
        var test = CreateVersionedTest("teacher-1");
        var groups = new FakeGroupServiceClient(CreateGroup());
        var service = await CreateServiceAsync(dbContext, groups, test);
        var controller = CreateController(service, "teacher-1", "Teacher");
        var request = CreateRequest(test.Id);

        var result = await controller.CreateAssignment(groups.Group.Id, request, CancellationToken.None);

        var assignment = Assert.IsType<TestAssignmentResponse>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        Assert.Equal(groups.Group.Id, assignment.GroupId);
        Assert.Equal("teacher-1", assignment.TeacherUserId);
        Assert.Equal(1, assignment.Test.VersionNumber);
        Assert.Equal("First revision", assignment.Test.Title);
        Assert.Equal("First task", Assert.Single(assignment.Test.Tasks).Title);
        Assert.Equal(request.AvailableFrom, assignment.AvailableFrom);
        Assert.Equal(request.Deadline, assignment.Deadline);
        var stored = Assert.Single(await dbContext.TestAssignments.ToListAsync());
        Assert.Equal(test.Versions[0].Id, stored.CourseTestVersionId);
        var catalog = await service.GetAssignableTestsAsync(groups.Group.Id, "teacher-1", CancellationToken.None);
        Assert.NotNull(catalog);
        Assert.Equal([1, 2], catalog.Select(version => version.VersionNumber).OrderBy(version => version));
    }

    [Fact]
    public async Task SamePublishedVersionCanBeAssignedAgainWithSeparateDeadline()
    {
        await using var dbContext = CreateDbContext();
        var test = CreateVersionedTest("teacher-1");
        var groups = new FakeGroupServiceClient(CreateGroup());
        var service = await CreateServiceAsync(dbContext, groups, test);
        var firstRequest = CreateRequest(test.Id);
        var secondRequest = CreateRequest(test.Id);
        secondRequest.Deadline = firstRequest.Deadline.AddDays(1);

        var first = await CreateAssignmentAsync(service, groups.Group.Id, firstRequest);
        var second = await CreateAssignmentAsync(service, groups.Group.Id, secondRequest);
        var assignments = await service.GetStudentAssignmentsAsync("student-1", CancellationToken.None);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal([first.Id, second.Id], assignments.Select(assignment => assignment.Id));
        Assert.Equal(firstRequest.Deadline, assignments[0].Deadline);
        Assert.Equal(secondRequest.Deadline, assignments[1].Deadline);
        Assert.All(assignments, assignment => Assert.Equal(1, assignment.Test.VersionNumber));
        Assert.Equal(2, await dbContext.TestAssignments.CountAsync());
    }

    [Fact]
    public async Task MembershipChangesImmediatelyAffectExistingAssignmentListAndDirectAccess()
    {
        await using var dbContext = CreateDbContext();
        var test = CreateVersionedTest("teacher-1");
        var groups = new FakeGroupServiceClient(CreateGroup());
        var service = await CreateServiceAsync(dbContext, groups, test);
        var assignment = await CreateAssignmentAsync(service, groups.Group.Id, CreateRequest(test.Id));

        Assert.Equal(assignment.Id, Assert.Single(await service.GetStudentAssignmentsAsync("student-1", CancellationToken.None)).Id);
        Assert.NotNull(await service.GetStudentAssignmentAsync(assignment.Id, "student-1", CancellationToken.None));
        Assert.Empty(await service.GetStudentAssignmentsAsync("student-2", CancellationToken.None));
        Assert.Null(await service.GetStudentAssignmentAsync(assignment.Id, "student-2", CancellationToken.None));

        groups.Group.StudentUserIds.Add("student-2");
        groups.Group.StudentUserIds.Remove("student-1");
        groups.Group.Name = "Renamed group";

        var newMemberAssignment = Assert.Single(await service.GetStudentAssignmentsAsync("student-2", CancellationToken.None));
        var directAssignment = await service.GetStudentAssignmentAsync(assignment.Id, "student-2", CancellationToken.None);
        Assert.Equal(assignment.Id, newMemberAssignment.Id);
        Assert.Equal("Renamed group", newMemberAssignment.GroupName);
        Assert.NotNull(directAssignment);
        Assert.Equal(assignment.Id, directAssignment.Id);
        Assert.Equal("Renamed group", directAssignment.GroupName);
        Assert.Empty(await service.GetStudentAssignmentsAsync("student-1", CancellationToken.None));
        Assert.Null(await service.GetStudentAssignmentAsync(assignment.Id, "student-1", CancellationToken.None));
        Assert.Equal(assignment.Id, Assert.Single(await dbContext.TestAssignments.ToListAsync()).Id);
    }

    [Fact]
    public async Task ChangingGroupTeacherTransfersNewAssignmentRightsAndKeepsOldAuthor()
    {
        await using var dbContext = CreateDbContext();
        var originalTest = CreateVersionedTest("teacher-1");
        var replacementTest = CreateVersionedTest("teacher-2");
        var groups = new FakeGroupServiceClient(CreateGroup());
        var service = await CreateServiceAsync(dbContext, groups, originalTest, replacementTest);
        var originalAssignment = await CreateAssignmentAsync(service, groups.Group.Id, CreateRequest(originalTest.Id));

        groups.Group.TeacherUserId = "teacher-2";
        var oldTeacherCatalog = await service.GetAssignableTestsAsync(groups.Group.Id, "teacher-1", CancellationToken.None);
        var oldTeacherAssignment = await service.CreateAssignmentAsync(
            groups.Group.Id, CreateRequest(originalTest.Id), "teacher-1", CancellationToken.None);
        var newTeacherCatalog = await service.GetAssignableTestsAsync(groups.Group.Id, "teacher-2", CancellationToken.None);
        var replacementAssignment = await CreateAssignmentAsync(
            service, groups.Group.Id, CreateRequest(replacementTest.Id), "teacher-2");
        var assignments = await service.GetGroupAssignmentsAsync(groups.Group.Id, "teacher-2", CancellationToken.None);
        var studentAssignments = await service.GetStudentAssignmentsAsync("student-1", CancellationToken.None);

        Assert.Null(oldTeacherCatalog);
        Assert.Equal(AssignmentOperationStatus.NotFound, oldTeacherAssignment.Status);
        Assert.NotNull(newTeacherCatalog);
        Assert.Equal(2, newTeacherCatalog.Count);
        Assert.All(newTeacherCatalog, test => Assert.Equal(replacementTest.Id.ToString(), test.Id));
        Assert.NotNull(assignments);
        Assert.Equal(2, assignments.Count);
        Assert.Equal("teacher-1", Assert.Single(assignments, assignment => assignment.Id == originalAssignment.Id).TeacherUserId);
        Assert.Equal("teacher-2", Assert.Single(assignments, assignment => assignment.Id == replacementAssignment.Id).TeacherUserId);
        Assert.Equal("teacher-1", Assert.Single(studentAssignments, assignment => assignment.Id == originalAssignment.Id).TeacherUserId);
        Assert.Equal(originalAssignment.Id, Assert.Single(await service.GetTeacherAssignmentsAsync("teacher-1", CancellationToken.None)).Id);
        Assert.Equal(replacementAssignment.Id, Assert.Single(await service.GetTeacherAssignmentsAsync("teacher-2", CancellationToken.None)).Id);
    }

    [Fact]
    public async Task AdminCanReadGroupAssignmentsWithoutAssignmentPrivileges()
    {
        await using var dbContext = CreateDbContext();
        var test = CreateVersionedTest("teacher-1");
        var groups = new FakeGroupServiceClient(CreateGroup());
        var service = await CreateServiceAsync(dbContext, groups, test);
        var assignment = await CreateAssignmentAsync(service, groups.Group.Id, CreateRequest(test.Id));
        var controller = CreateController(service, "admin-1", "Admin");

        var result = await controller.GetAssignments(groups.Group.Id, CancellationToken.None);

        var assignments = Assert.IsType<List<TestAssignmentResponse>>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(assignment.Id, Assert.Single(assignments).Id);
        Assert.Equal("teacher-1", assignments[0].TeacherUserId);
    }

    [Fact]
    public async Task NewGroupTeacherReceivesAssignmentSummaryWithoutAuthorsPrivateGradingData()
    {
        await using var dbContext = CreateDbContext();
        var test = CreateVersionedTest("teacher-1");
        var version = test.Versions[0];
        version.ContentRevision = 29;
        version.LlmModelId = 44;
        var template = new FeedbackTemplateDto
        {
            Id = Guid.NewGuid(),
            Name = "Private feedback template",
            CustomInstruction = "Private feedback instruction"
        };
        version.FeedbackTemplateId = template.Id;
        version.FeedbackTemplateSnapshotJson = FeedbackTemplateJson.SerializeSnapshot(template);
        version.GradingExamples =
        [
            new GradingExample
            {
                TaskPrompt = "Private calibration prompt",
                StudentAnswer = "Private example answer",
                Feedback = "Private grading feedback"
            }
        ];
        var task = version.Tasks[0];
        task.Type = TestTaskType.SingleChoice;
        task.CheckMode = TestTaskCheckMode.Auto;
        task.Prompt = "Private task prompt";
        var correctOptionId = Guid.NewGuid();
        task.Options =
        [
            new AnswerOption
            {
                Id = correctOptionId,
                TestTaskId = task.Id,
                Text = "Private correct answer",
                IsCorrect = true
            }
        ];
        version.Tasks.Add(new TestTask
        {
            Id = Guid.NewGuid(),
            CourseTestVersionId = version.Id,
            Title = "Private hidden task",
            IsHidden = true,
            MaxPoints = 70
        });
        test.Versions.Add(new CourseTestVersion
        {
            Id = Guid.NewGuid(),
            CourseTestId = test.Id,
            VersionNumber = 3,
            Title = "Private draft title",
            Status = CourseTestStatus.Draft
        });
        var groups = new FakeGroupServiceClient(CreateGroup());
        var service = await CreateServiceAsync(dbContext, groups, test);
        var assignment = await CreateAssignmentAsync(service, groups.Group.Id, CreateRequest(test.Id));
        groups.Group.TeacherUserId = "teacher-2";
        var controller = CreateController(service, "teacher-2", "Teacher");

        var result = await controller.GetAssignments(groups.Group.Id, CancellationToken.None);
        var assignments = Assert.IsType<List<TestAssignmentResponse>>(Assert.IsType<OkObjectResult>(result.Result).Value);
        var summary = Assert.Single(assignments);

        Assert.Equal(assignment.Id, summary.Id);
        Assert.Equal("teacher-1", summary.TeacherUserId);
        Assert.Equal("First revision", summary.Test.Title);
        Assert.Equal(1, summary.Test.VersionNumber);
        Assert.Equal(10m, summary.Test.TotalPoints);
        Assert.Empty(summary.Test.Tasks);
        Assert.Empty(summary.Test.GradingExamples);
        Assert.Empty(summary.Test.Versions);
        Assert.Null(summary.Test.FeedbackTemplate);
        Assert.Null(summary.Test.FeedbackTemplateId);
        Assert.Null(summary.Test.LlmModelId);
        Assert.Null(summary.Test.PublishedVersionNumber);
        Assert.False(summary.Test.HasDraft);
        Assert.Equal(0, summary.Test.ContentRevision);
        Assert.DoesNotContain("Private", JsonSerializer.Serialize(summary));
        Assert.IsType<NotFoundResult>((await controller.GetAssignment(assignment.Id, CancellationToken.None)).Result);

        var authorAssignment = await service.GetAssignmentAsync(assignment.Id, includeHidden: true, CancellationToken.None);
        Assert.NotNull(authorAssignment);
        Assert.Equal("Private example answer", Assert.Single(authorAssignment.Test.GradingExamples).StudentAnswer);
        Assert.Equal(template.CustomInstruction, authorAssignment.Test.FeedbackTemplate?.CustomInstruction);
        Assert.Contains(authorAssignment.Test.Tasks, returnedTask => returnedTask.IsHidden);
        Assert.Equal([correctOptionId.ToString()], Assert.Single(authorAssignment.Test.Tasks,
            returnedTask => returnedTask.Id == task.Id.ToString()).CorrectOptionIds);
    }

    [Fact]
    public async Task GroupServiceFailurePreventsAssignmentCreation()
    {
        await using var dbContext = CreateDbContext();
        var test = CreateVersionedTest("teacher-1");
        var groups = new FakeGroupServiceClient(CreateGroup()) { FailRequests = true };
        var service = await CreateServiceAsync(dbContext, groups, test);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => service.CreateAssignmentAsync(
            groups.Group.Id, CreateRequest(test.Id), "teacher-1", CancellationToken.None));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
        Assert.Empty(await dbContext.TestAssignments.ToListAsync());
    }

    [Fact]
    public async Task HistoricalAssignmentRemainsAvailableWithoutGroupService()
    {
        await using var dbContext = CreateDbContext();
        var test = CreateVersionedTest("teacher-1");
        var groups = new FakeGroupServiceClient(CreateGroup());
        var service = await CreateServiceAsync(dbContext, groups, test);
        var assignment = await CreateAssignmentAsync(service, groups.Group.Id, CreateRequest(test.Id));
        var groupRequests = groups.GroupRequests;
        groups.FailRequests = true;

        var historical = await service.GetAssignmentAsync(assignment.Id, includeHidden: false, CancellationToken.None);

        Assert.NotNull(historical);
        Assert.Equal(assignment.Id, historical.Id);
        Assert.Equal(assignment.GroupName, historical.GroupName);
        Assert.Equal("teacher-1", historical.TeacherUserId);
        Assert.Equal("First revision", historical.Test.Title);
        Assert.Equal(groupRequests, groups.GroupRequests);
        Assert.Equal(0, groups.StudentGroupRequests);
    }

    [Theory]
    [InlineData("Admin", "admin-1", true)]
    [InlineData("Teacher", "teacher-1", true)]
    [InlineData("Teacher", "teacher-2", false)]
    public async Task HistoricalAssignmentUsesAuthorScopeAfterGroupTeacherChanges(
        string role,
        string userId,
        bool allowed)
    {
        await using var dbContext = CreateDbContext();
        var test = CreateVersionedTest("teacher-1");
        var groups = new FakeGroupServiceClient(CreateGroup());
        var service = await CreateServiceAsync(dbContext, groups, test);
        var assignment = await CreateAssignmentAsync(service, groups.Group.Id, CreateRequest(test.Id));
        groups.Group.TeacherUserId = "teacher-2";
        groups.FailRequests = true;
        var controller = CreateController(service, userId, role);
        var groupRequests = groups.GroupRequests;

        var result = await controller.GetAssignment(assignment.Id, CancellationToken.None);

        if (allowed)
        {
            var historical = Assert.IsType<TestAssignmentResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
            Assert.Equal(assignment.Id, historical.Id);
            Assert.Equal("teacher-1", historical.TeacherUserId);
        }
        else
        {
            Assert.IsType<NotFoundResult>(result.Result);
        }

        Assert.Equal(groupRequests, groups.GroupRequests);
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

    private static async Task<TestAssignmentService> CreateServiceAsync(
        TeachingDbContext dbContext,
        FakeGroupServiceClient groups,
        params CourseTest[] tests)
    {
        dbContext.Tests.AddRange(tests);
        await dbContext.SaveChangesAsync();
        return new TestAssignmentService(dbContext, groups);
    }

    private static StudyGroupResponse CreateGroup()
    {
        return new StudyGroupResponse
        {
            Id = Guid.NewGuid(),
            Name = "Group 1",
            TeacherUserId = "teacher-1",
            StudentUserIds = ["student-1"]
        };
    }

    private static TestAssignmentsController CreateController(
        TestAssignmentService service,
        string? userId,
        params string[] roles)
    {
        var claims = roles.Select(role => new Claim(ClaimTypes.Role, role)).ToList();
        if (userId is not null)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
        }

        return new TestAssignmentsController(service)
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

    private static CreateTestAssignmentRequest CreateRequest(Guid testId)
    {
        return new CreateTestAssignmentRequest
        {
            TestId = testId,
            TestRevision = 1,
            AvailableFrom = DateTimeOffset.UtcNow.AddMinutes(-1),
            Deadline = DateTimeOffset.UtcNow.AddDays(1)
        };
    }

    private static async Task<TestAssignmentResponse> CreateAssignmentAsync(
        TestAssignmentService service,
        Guid groupId,
        CreateTestAssignmentRequest request,
        string teacherUserId = "teacher-1")
    {
        var result = await service.CreateAssignmentAsync(groupId, request, teacherUserId, CancellationToken.None);
        Assert.Equal(AssignmentOperationStatus.Success, result.Status);
        return Assert.IsType<TestAssignmentResponse>(result.Value);
    }

    private class FakeGroupServiceClient : IGroupServiceClient
    {
        public FakeGroupServiceClient(StudyGroupResponse group)
        {
            Group = group;
        }

        public StudyGroupResponse Group { get; }
        public bool FailRequests { get; set; }
        public int GroupRequests { get; private set; }
        public int StudentGroupRequests { get; private set; }

        public Task<StudyGroupResponse?> GetGroupAsync(Guid groupId, CancellationToken cancellationToken)
        {
            GroupRequests++;
            ThrowIfUnavailable();
            return Task.FromResult<StudyGroupResponse?>(Group.Id == groupId ? Group : null);
        }

        public Task<List<StudyGroupResponse>> GetStudentGroupsAsync(string studentUserId, CancellationToken cancellationToken)
        {
            StudentGroupRequests++;
            ThrowIfUnavailable();
            return Task.FromResult<List<StudyGroupResponse>>(Group.StudentUserIds.Contains(studentUserId) ? [Group] : []);
        }

        private void ThrowIfUnavailable()
        {
            if (FailRequests)
            {
                throw new HttpRequestException("GroupService is unavailable.", null, HttpStatusCode.ServiceUnavailable);
            }
        }
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

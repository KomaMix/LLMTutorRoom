using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TeachingService.Contracts.Enums;
using TeachingService.Contracts.Models;
using TeachingService.Contracts.Requests;
using TeachingService.Data;
using TeachingService.Enums;
using TeachingService.Services.IntegrationEvents;
using TeachingService.Models;
using TeachingService.Services;

namespace TeachingService.Tests;

public sealed class TestVersioningTests
{
    [Fact]
    public async Task GradingExamples_AreClonedPublishedAndKeptWithTheirOriginalVersion()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var generalExample = new GradingExampleDto
        {
            TaskPrompt = "Explain an algorithm.",
            StudentAnswer = "A general example answer.",
            Score = 3,
            MaxScore = 5,
            Feedback = "General grading explanation."
        };
        var taskExample = generalExample with
        {
            TaskPrompt = "",
            StudentAnswer = "A task-specific example answer.",
            Score = 6,
            MaxScore = 10,
            Feedback = "Task grading explanation."
        };
        var testRequest = CreateTestRequest("Examples v1");
        testRequest.GradingExamples = [generalExample];
        var created = await service.CreateTestAsync(testRequest, "teacher", CancellationToken.None);
        var testId = Guid.Parse(created.Id);
        dbContext.ChangeTracker.Clear();
        var taskRequest = new CreateTaskRequest
        {
            Type = TestTaskType.FreeText,
            CheckMode = TestTaskCheckMode.Llm,
            Title = "Explain binary search",
            Prompt = "Explain the algorithm and its complexity.",
            MaxPoints = 10,
            GradingExamples = [taskExample]
        };
        var added = await service.AddTaskAsync(testId, 1, 0, taskRequest, "teacher", CancellationToken.None);
        Assert.Equal(CatalogOperationStatus.Success, added.Status);
        dbContext.ChangeTracker.Clear();
        var published = await service.PublishVersionAsync(testId, 1, 1, "teacher", CancellationToken.None);
        Assert.Equal(CatalogOperationStatus.Success, published.Status);
        dbContext.ChangeTracker.Clear();

        var draft = await service.CreateDraftVersionAsync(testId, 1, "teacher", CancellationToken.None);
        Assert.Equal(CatalogOperationStatus.Success, draft.Status);
        Assert.Equal(generalExample, Assert.Single(draft.Value!.GradingExamples!));
        var draftTask = Assert.Single(draft.Value.Tasks);
        Assert.Equal(taskExample, Assert.Single(draftTask.GradingExamples!));
        dbContext.ChangeTracker.Clear();

        testRequest.GradingExamples = [generalExample with { Score = 4, Feedback = "Updated general example." }];
        var updatedTest = await service.UpdateTestAsync(testId, 2, 0, testRequest, "teacher", CancellationToken.None);
        Assert.Equal(CatalogOperationStatus.Success, updatedTest.Status);
        dbContext.ChangeTracker.Clear();
        taskRequest.GradingExamples = [taskExample with { Score = 7, Feedback = "Updated task example." }];
        var updatedTask = await service.UpdateTaskAsync(
            testId, 2, 1, Guid.Parse(draftTask.Id), taskRequest, "teacher", CancellationToken.None);
        Assert.Equal(CatalogOperationStatus.Success, updatedTask.Status);
        dbContext.ChangeTracker.Clear();
        var publishedV2 = await service.PublishVersionAsync(testId, 2, 2, "teacher", CancellationToken.None);
        Assert.Equal(CatalogOperationStatus.Success, publishedV2.Status);
        dbContext.ChangeTracker.Clear();

        var oldVersion = await service.GetTeacherTestVersionAsync(testId, 1, "teacher", true, CancellationToken.None);
        Assert.Equal(generalExample, Assert.Single(oldVersion!.GradingExamples!));
        Assert.Equal(taskExample, Assert.Single(Assert.Single(oldVersion.Tasks).GradingExamples!));
        Assert.Equal(testRequest.GradingExamples, publishedV2.Value!.GradingExamples);
        Assert.Equal(taskRequest.GradingExamples, Assert.Single(publishedV2.Value.Tasks).GradingExamples);

        var policies = await dbContext.TestReviewPolicyRevisions.OrderBy(policy => policy.Revision).ToListAsync();
        Assert.Equal(2, policies.Count);
        using var oldPolicy = JsonDocument.Parse(policies[0].Payload);
        using var newPolicy = JsonDocument.Parse(policies[1].Payload);
        Assert.Equal(3m, oldPolicy.RootElement.GetProperty("gradingExamples")[0].GetProperty("score").GetDecimal());
        Assert.Equal(6m, oldPolicy.RootElement.GetProperty("tasks")[0].GetProperty("gradingExamples")[0].GetProperty("score").GetDecimal());
        Assert.Equal(4m, newPolicy.RootElement.GetProperty("gradingExamples")[0].GetProperty("score").GetDecimal());
        Assert.Equal(7m, newPolicy.RootElement.GetProperty("tasks")[0].GetProperty("gradingExamples")[0].GetProperty("score").GetDecimal());
        Assert.Equal(policies[0].Payload,
            (await dbContext.IntegrationOutboxMessages.SingleAsync(message => message.Id == policies[0].EventId)).Payload);
    }

    [Theory]
    [InlineData(-1, 5)]
    [InlineData(6, 5)]
    [InlineData(0, 0)]
    public async Task AddTask_RejectsInvalidExampleScoresWithoutChangingDraft(int score, int maxScore)
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var test = await service.CreateTestAsync(CreateTestRequest("Examples"), "teacher", CancellationToken.None);
        var result = await service.AddTaskAsync(Guid.Parse(test.Id), 1, 0, new CreateTaskRequest
        {
            Type = TestTaskType.FreeText,
            Title = "Task",
            Prompt = "Explain.",
            GradingExamples =
            [
                new GradingExampleDto
                {
                    StudentAnswer = "Example answer.",
                    Score = score,
                    MaxScore = maxScore,
                    Feedback = "Teacher feedback."
                }
            ]
        }, "teacher", CancellationToken.None);

        Assert.Equal(CatalogOperationStatus.ValidationFailed, result.Status);
        Assert.Empty(dbContext.TestTasks);
        Assert.Equal(0, (await dbContext.TestVersions.SingleAsync()).ContentRevision);
    }

    [Fact]
    public async Task CreateTest_RequiresTaskPromptInGeneralExamples()
    {
        await using var dbContext = CreateDbContext();
        var request = CreateTestRequest("Examples");
        request.GradingExamples =
        [
            new GradingExampleDto { StudentAnswer = "Answer", Score = 1, MaxScore = 2, Feedback = "Feedback" }
        ];

        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService(dbContext).CreateTestAsync(request, "teacher", CancellationToken.None));
        Assert.Empty(dbContext.Tests);
    }

    [Fact]
    public async Task AddTask_FreeTextRequiresLlmOrManualReview()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var created = await service.CreateTestAsync(
            CreateTestRequest("Free-text modes"),
            "teacher",
            CancellationToken.None);

        var result = await service.AddTaskAsync(
            Guid.Parse(created.Id),
            expectedVersionNumber: 1,
            expectedContentRevision: 0,
            new CreateTaskRequest
            {
                Type = TestTaskType.FreeText,
                CheckMode = TestTaskCheckMode.Auto,
                Title = "Essay",
                Prompt = "Explain.",
                MaxPoints = 5
            },
            "teacher",
            CancellationToken.None);

        Assert.Equal(CatalogOperationStatus.ValidationFailed, result.Status);
        Assert.Empty(dbContext.TestTasks);
    }

    [Fact]
    public async Task Publish_IsTheOnlyOperationThatCreatesPolicyAndPreservesOldVersion()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var created = await service.CreateTestAsync(
            CreateTestRequest("Version 1"),
            "teacher",
            CancellationToken.None);
        dbContext.ChangeTracker.Clear();

        var task = await service.AddTaskAsync(
            Guid.Parse(created.Id),
            expectedVersionNumber: 1,
            expectedContentRevision: 0,
            CreateChoiceTaskRequest("Task v1"),
            "teacher",
            CancellationToken.None);
        dbContext.ChangeTracker.Clear();

        Assert.True(
            task.Status == CatalogOperationStatus.Success,
            $"Expected task creation to succeed, but got {task.Status}: {task.Error}");
        Assert.Empty(dbContext.TestReviewPolicyRevisions);
        Assert.Empty(dbContext.IntegrationOutboxMessages);

        var firstPublication = await service.PublishVersionAsync(
            Guid.Parse(created.Id),
            versionNumber: 1,
            expectedContentRevision: 1,
            "teacher",
            CancellationToken.None);
        dbContext.ChangeTracker.Clear();

        Assert.Equal(CatalogOperationStatus.Success, firstPublication.Status);
        Assert.Equal(CourseTestStatus.Published, firstPublication.Value!.Status);
        Assert.Single(dbContext.TestReviewPolicyRevisions);
        Assert.Single(dbContext.IntegrationOutboxMessages);

        var immutableUpdate = await service.UpdateTestAsync(
            Guid.Parse(created.Id),
            expectedVersionNumber: 1,
            expectedContentRevision: 1,
            CreateTestRequest("Must not replace v1"),
            "teacher",
            CancellationToken.None);
        dbContext.ChangeTracker.Clear();
        Assert.Equal(CatalogOperationStatus.Conflict, immutableUpdate.Status);

        var staleSourceDraft = await service.CreateDraftVersionAsync(
            Guid.Parse(created.Id),
            expectedPublishedVersionNumber: 2,
            "teacher",
            CancellationToken.None);
        dbContext.ChangeTracker.Clear();
        Assert.Equal(CatalogOperationStatus.Conflict, staleSourceDraft.Status);

        var draft = await service.CreateDraftVersionAsync(
            Guid.Parse(created.Id),
            expectedPublishedVersionNumber: 1,
            "teacher",
            CancellationToken.None);
        dbContext.ChangeTracker.Clear();

        Assert.True(
            draft.Status == CatalogOperationStatus.Success,
            $"Expected draft creation to succeed, but got {draft.Status}: {draft.Error}");
        Assert.Equal(2, draft.Value!.VersionNumber);
        Assert.Equal(CourseTestStatus.Draft, draft.Value.Status);
        Assert.NotEqual(
            Assert.Single(firstPublication.Value.Tasks).Id,
            Assert.Single(draft.Value.Tasks).Id);
        Assert.Single(dbContext.TestReviewPolicyRevisions);

        var staleDraftUpdate = await service.UpdateTestAsync(
            Guid.Parse(created.Id),
            expectedVersionNumber: 1,
            expectedContentRevision: 0,
            CreateTestRequest("Must not replace v2"),
            "teacher",
            CancellationToken.None);
        dbContext.ChangeTracker.Clear();
        Assert.Equal(CatalogOperationStatus.Conflict, staleDraftUpdate.Status);

        var draftTaskId = Guid.Parse(Assert.Single(draft.Value.Tasks).Id);
        var taskUpdate = await service.UpdateTaskAsync(
            Guid.Parse(created.Id),
            expectedVersionNumber: 2,
            expectedContentRevision: 0,
            draftTaskId,
            CreateChoiceTaskRequest("Task v2"),
            "teacher",
            CancellationToken.None);
        dbContext.ChangeTracker.Clear();
        Assert.Equal(CatalogOperationStatus.Success, taskUpdate.Status);
        Assert.Equal("Task v2", taskUpdate.Value!.Title);
        Assert.Single(dbContext.TestReviewPolicyRevisions);

        var stalePublication = await service.PublishVersionAsync(
            Guid.Parse(created.Id),
            versionNumber: 2,
            expectedContentRevision: 0,
            "teacher",
            CancellationToken.None);
        dbContext.ChangeTracker.Clear();
        Assert.Equal(CatalogOperationStatus.Conflict, stalePublication.Status);
        Assert.Single(dbContext.TestReviewPolicyRevisions);

        var draftUpdate = await service.UpdateTestAsync(
            Guid.Parse(created.Id),
            expectedVersionNumber: 2,
            expectedContentRevision: 1,
            CreateTestRequest("Version 2"),
            "teacher",
            CancellationToken.None);
        dbContext.ChangeTracker.Clear();
        Assert.Equal(CatalogOperationStatus.Success, draftUpdate.Status);
        Assert.Single(dbContext.TestReviewPolicyRevisions);

        var secondPublication = await service.PublishVersionAsync(
            Guid.Parse(created.Id),
            versionNumber: 2,
            expectedContentRevision: 2,
            "teacher",
            CancellationToken.None);
        dbContext.ChangeTracker.Clear();

        Assert.Equal(CatalogOperationStatus.Success, secondPublication.Status);
        Assert.Equal(2, dbContext.TestReviewPolicyRevisions.Count());
        Assert.Equal(2, dbContext.IntegrationOutboxMessages.Count());

        var oldVersion = await service.GetTestAsync(
            Guid.Parse(created.Id),
            versionNumber: 1,
            includeHidden: true,
            CancellationToken.None);
        Assert.NotNull(oldVersion);
        Assert.Equal(CourseTestStatus.Superseded, oldVersion.Status);
        Assert.Equal("Version 1", oldVersion.Title);
        Assert.Equal("Task v1", Assert.Single(oldVersion.Tasks).Title);
    }

    [Fact]
    public async Task DeletedDraftNumber_IsNeverReused()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var created = await service.CreateTestAsync(
            CreateTestRequest("Version 1"),
            "teacher",
            CancellationToken.None);
        dbContext.ChangeTracker.Clear();
        await service.AddTaskAsync(
            Guid.Parse(created.Id),
            expectedVersionNumber: 1,
            expectedContentRevision: 0,
            CreateChoiceTaskRequest("Task"),
            "teacher",
            CancellationToken.None);
        dbContext.ChangeTracker.Clear();
        await service.PublishVersionAsync(
            Guid.Parse(created.Id),
            1,
            expectedContentRevision: 1,
            "teacher",
            CancellationToken.None);
        dbContext.ChangeTracker.Clear();

        var draft2 = await service.CreateDraftVersionAsync(
            Guid.Parse(created.Id),
            expectedPublishedVersionNumber: 1,
            "teacher",
            CancellationToken.None);
        dbContext.ChangeTracker.Clear();
        Assert.True(
            draft2.Status == CatalogOperationStatus.Success,
            $"Expected draft creation to succeed, but got {draft2.Status}: {draft2.Error}");
        Assert.Equal(2, draft2.Value!.VersionNumber);

        var deleted = await service.DeleteDraftVersionAsync(
            Guid.Parse(created.Id),
            2,
            expectedContentRevision: 0,
            "teacher",
            CancellationToken.None);
        dbContext.ChangeTracker.Clear();
        Assert.Equal(CatalogOperationStatus.Success, deleted.Status);

        var draft3 = await service.CreateDraftVersionAsync(
            Guid.Parse(created.Id),
            expectedPublishedVersionNumber: 1,
            "teacher",
            CancellationToken.None);
        Assert.True(
            draft3.Status == CatalogOperationStatus.Success,
            $"Expected draft creation to succeed, but got {draft3.Status}: {draft3.Error}");
        Assert.Equal(3, draft3.Value!.VersionNumber);
    }

    [Fact]
    public async Task Publish_WithUnsupportedTaskEnum_ReturnsValidationFailure()
    {
        await using var dbContext = CreateDbContext();
        var testId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var draft = new CourseTestVersion
        {
            Id = versionId,
            CourseTestId = testId,
            VersionNumber = 1,
            Status = CourseTestStatus.Draft,
            Title = "Invalid task draft",
            Subject = "Subject",
            Deadline = DateTimeOffset.UtcNow.AddDays(1),
            TimeLimitMinutes = 30,
            LlmModelKey = "model",
            Tasks =
            [
                new TestTask
                {
                    Id = Guid.NewGuid(),
                    CourseTestVersionId = versionId,
                    Type = TestTaskType.FreeText,
                    CheckMode = (TestTaskCheckMode)999,
                    Title = "Task",
                    Prompt = "Answer.",
                    MaxPoints = 1
                }
            ]
        };
        var test = new CourseTest
        {
            Id = testId,
            TeacherUserId = "teacher",
            NextVersionNumber = 2,
            Versions = [draft]
        };
        draft.CourseTest = test;
        dbContext.Tests.Add(test);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var result = await CreateService(dbContext).PublishVersionAsync(
            testId,
            versionNumber: 1,
            expectedContentRevision: 0,
            "teacher",
            CancellationToken.None);

        Assert.Equal(CatalogOperationStatus.ValidationFailed, result.Status);
        Assert.Empty(dbContext.TestReviewPolicyRevisions);
        Assert.Empty(dbContext.IntegrationOutboxMessages);
    }

    [Fact]
    public async Task Publish_WithUnsupportedFreeTextReviewMode_ReturnsValidationFailure()
    {
        await using var dbContext = CreateDbContext();
        var testId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var draft = new CourseTestVersion
        {
            Id = versionId,
            CourseTestId = testId,
            VersionNumber = 1,
            Status = CourseTestStatus.Draft,
            Title = "Invalid review mode",
            Subject = "Subject",
            Deadline = DateTimeOffset.UtcNow.AddDays(1),
            TimeLimitMinutes = 30,
            LlmModelKey = "model",
            Tasks =
            [
                new TestTask
                {
                    Id = Guid.NewGuid(),
                    CourseTestVersionId = versionId,
                    Type = TestTaskType.FreeText,
                    CheckMode = TestTaskCheckMode.Auto,
                    Title = "Essay",
                    Prompt = "Explain.",
                    MaxPoints = 5
                }
            ]
        };
        var test = new CourseTest
        {
            Id = testId,
            TeacherUserId = "teacher",
            NextVersionNumber = 2,
            Versions = [draft]
        };
        draft.CourseTest = test;
        dbContext.Tests.Add(test);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var result = await CreateService(dbContext).PublishVersionAsync(
            testId,
            versionNumber: 1,
            expectedContentRevision: 0,
            "teacher",
            CancellationToken.None);

        Assert.Equal(CatalogOperationStatus.ValidationFailed, result.Status);
        Assert.Empty(dbContext.TestReviewPolicyRevisions);
        Assert.Empty(dbContext.IntegrationOutboxMessages);
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

    private static TeachingCatalogService CreateService(TeachingDbContext dbContext)
    {
        return new TeachingCatalogService(
            dbContext,
            new ReviewPolicyOutboxWriter(dbContext));
    }

    private static CreateTestRequest CreateTestRequest(string title)
    {
        return new CreateTestRequest
        {
            Title = title,
            Subject = "Subject",
            Status = CourseTestStatus.Draft,
            Deadline = DateTimeOffset.UtcNow.AddDays(7),
            TimeLimitMinutes = 45,
            Summary = "Summary",
            LlmModelKey = "model"
        };
    }

    private static CreateTaskRequest CreateChoiceTaskRequest(string title)
    {
        return new CreateTaskRequest
        {
            Type = TestTaskType.SingleChoice,
            CheckMode = TestTaskCheckMode.Auto,
            Title = title,
            Prompt = "Choose.",
            MaxPoints = 2,
            Options = ["Correct", "Wrong"],
            CorrectOptionIndexes = [0]
        };
    }
}

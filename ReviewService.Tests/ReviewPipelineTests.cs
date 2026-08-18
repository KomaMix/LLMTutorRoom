using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OptionsFactory = Microsoft.Extensions.Options.Options;
using ReviewService.Options;
using ReviewService.Contracts.Enums;
using ReviewService.Contracts.Events;
using ReviewService.Contracts.Requests;
using ReviewService.Contracts.Responses;
using ReviewService.Data;
using ReviewService.Enums;
using ReviewService.Helpers;
using ReviewService.Interfaces;
using ReviewService.Models.ModelAccess;
using ReviewService.Models.Reviews;
using ReviewService.Services;

namespace ReviewService.Tests;

public sealed class ReviewPipelineTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AttemptBeforePolicy_IsHeldAndReviewedWhenPolicyArrives()
    {
        await using var dbContext = CreateDbContext();
        var queue = new FakeReviewQueuePublisher();
        var handler = CreateEventHandler(dbContext, queue);
        var attempt = CreateAttempt();

        await handler.HandleAsync(attempt, CancellationToken.None);

        Assert.Empty(dbContext.Reviews);
        Assert.Single(dbContext.PendingSubmissions);

        await handler.HandleAsync(CreatePolicy(), CancellationToken.None);

        var review = await dbContext.Reviews.Include(item => item.TaskResults).SingleAsync();
        Assert.Empty(dbContext.PendingSubmissions);
        Assert.Equal(ReviewStatus.Checked, review.Status);
        Assert.Equal(2m, review.Score);
        Assert.Equal("a", review.TaskResults.Single().StudentAnswer);
        Assert.Equal(2, await dbContext.InboxMessages.CountAsync());
        Assert.Empty(queue.Messages);
    }

    [Fact]
    public async Task DuplicateEventAndAttempt_DoNotCreateDuplicateReview()
    {
        await using var dbContext = CreateDbContext();
        var handler = CreateEventHandler(dbContext, new FakeReviewQueuePublisher());
        var policy = CreatePolicy();
        var attempt = CreateAttempt();

        await handler.HandleAsync(policy, CancellationToken.None);
        await handler.HandleAsync(attempt, CancellationToken.None);
        await handler.HandleAsync(attempt, CancellationToken.None);
        await handler.HandleAsync(attempt with { EventId = Guid.NewGuid() }, CancellationToken.None);

        Assert.Single(dbContext.Reviews);
        Assert.Empty(dbContext.PendingSubmissions);
        Assert.Equal(3, await dbContext.InboxMessages.CountAsync());
    }

    [Fact]
    public void JsonHelper_UsesCamelCaseEnumNames()
    {
        var policy = CreatePolicy(new ReviewTaskPolicySnapshot(
            "essay",
            ReviewTaskType.FreeText,
            ReviewCheckMode.Llm,
            "Essay",
            "Explain.",
            5m,
            0,
            []));

        var json = JsonSerializer.Serialize(policy, JsonHelper.Options);
        var deserialized = JsonSerializer.Deserialize<TestReviewPolicyPublishedV1>(
            json,
            JsonHelper.Options);

        Assert.Contains("\"type\":\"freeText\"", json);
        Assert.Contains("\"checkMode\":\"llm\"", json);
        Assert.NotNull(deserialized);
        var task = Assert.Single(deserialized.Tasks);
        Assert.Equal(ReviewTaskType.FreeText, task.Type);
        Assert.Equal(ReviewCheckMode.Llm, task.CheckMode);
    }

    [Fact]
    public void MultipleChoiceScoring_AppliesWrongAnswerPenalty()
    {
        var scoring = new ReviewScoringService();
        var task = new ReviewTaskPolicySnapshot(
            "multi",
            ReviewTaskType.MultipleChoice,
            ReviewCheckMode.Auto,
            "Multiple",
            "Choose all.",
            4m,
            1m,
            [
                new ReviewAnswerOptionSnapshot("a", "A", true),
                new ReviewAnswerOptionSnapshot("b", "B", true),
                new ReviewAnswerOptionSnapshot("x", "X", false)
            ]);

        var result = scoring.CreateInitialResult(task, "a|x", Now);

        Assert.Equal(ReviewTaskStatus.Succeeded, result.Status);
        Assert.Equal(1m, result.Score);
        Assert.Equal(4m, result.MaxScore);
    }

    [Fact]
    public async Task ManualUpdate_RequiresOwningTeacher()
    {
        await using var dbContext = CreateDbContext();
        var review = new Review
        {
            AttemptId = 10,
            TestId = "test-1",
            TestRevision = 1,
            TestTitle = "Test",
            TeacherUserId = "teacher-owner",
            StudentUserId = "student",
            Status = ReviewStatus.ManualReview,
            SubmittedAt = Now,
            MaxScore = 3m,
            TaskResults =
            [
                new ReviewTask
                {
                    TaskId = "manual",
                    TaskType = ReviewTaskType.FreeText,
                    TaskTitle = "Manual",
                    TaskPrompt = "Explain.",
                    StudentAnswer = "An answer.",
                    CheckMode = ReviewCheckMode.Manual,
                    Status = ReviewTaskStatus.ManualReview,
                    MaxScore = 3m
                }
            ]
        };
        dbContext.Reviews.Add(review);
        await dbContext.SaveChangesAsync();
        var service = new ReviewQueryService(
            dbContext,
            new ReviewScoringService());
        var request = new UpdateManualTaskReviewRequest
        {
            Score = 2.5m,
            Feedback = "Good.",
            Findings = ["Reasoned."]
        };

        var forbidden = await service.UpdateManualTaskReviewAsync(
            review.Id,
            "manual",
            "another-teacher",
            request,
            CancellationToken.None);
        var updated = await service.UpdateManualTaskReviewAsync(
            review.Id,
            "manual",
            "teacher-owner",
            request,
            CancellationToken.None);

        Assert.Equal(ManualReviewUpdateStatus.Forbidden, forbidden.Status);
        Assert.Equal(ManualReviewUpdateStatus.Updated, updated.Status);
        Assert.Equal(ReviewStatus.Checked, review.Status);
        Assert.Equal(1, review.ProcessingGeneration);
        Assert.Equal(2.5m, review.Score);
        Assert.Equal(ReviewTaskStatus.Succeeded, review.TaskResults.Single().Status);
    }

    [Fact]
    public async Task QuotaConsumption_UsesRequestedCountAndRejectsOverflow()
    {
        await using var dbContext = CreateDbContext();
        dbContext.TeacherModelAccesses.Add(new TeacherModelAccess
        {
            TeacherUserId = "teacher",
            ModelKey = "model",
            IsEnabled = true,
            PeriodSeconds = 3600,
            MaxChecks = 3,
            CreatedAt = Now,
            UpdatedAt = Now
        });
        await dbContext.SaveChangesAsync();
        var service = new TeacherModelAccessService(
            dbContext,
            new FakeCatalogClient());

        var first = await service.TryConsumeChecksAsync(
            "teacher",
            "model",
            2,
            CancellationToken.None);
        var second = await service.TryConsumeChecksAsync(
            "teacher",
            "model",
            2,
            CancellationToken.None);

        Assert.Equal(ModelQuotaConsumptionStatus.Allowed, first.Status);
        Assert.Equal(1, first.RemainingChecks);
        Assert.Equal(ModelQuotaConsumptionStatus.LimitExceeded, second.Status);
        Assert.Equal(1, second.RemainingChecks);
        Assert.Equal(2, (await dbContext.TeacherModelUsages.SingleAsync()).UsedChecks);
    }

    [Fact]
    public async Task TeacherAccessUpsert_CreatesAndUpdatesOneAccessRecord()
    {
        await using var dbContext = CreateDbContext();
        var service = new TeacherModelAccessService(
            dbContext,
            new FakeCatalogClient());

        var created = await service.UpsertTeacherAccessAsync(
            "teacher",
            " model ",
            new UpsertTeacherModelAccessRequest
            {
                IsEnabled = true,
                PeriodSeconds = 3600,
                MaxChecks = 10
            },
            CancellationToken.None);
        var updated = await service.UpsertTeacherAccessAsync(
            "teacher",
            "model",
            new UpsertTeacherModelAccessRequest
            {
                IsEnabled = false,
                PeriodSeconds = 7200,
                MaxChecks = 25
            },
            CancellationToken.None);

        var stored = await dbContext.TeacherModelAccesses.SingleAsync();
        Assert.NotNull(created);
        Assert.NotNull(updated);
        Assert.Equal("model", stored.ModelKey);
        Assert.False(stored.IsEnabled);
        Assert.Equal(7200, stored.PeriodSeconds);
        Assert.Equal(25, stored.MaxChecks);
    }

    [Fact]
    public async Task ExhaustedQuota_KeepsLlmTaskQueuedForLaterRetry()
    {
        await using var dbContext = CreateDbContext();
        const int periodSeconds = int.MaxValue;
        var now = DateTimeOffset.UtcNow;
        dbContext.TeacherModelAccesses.Add(new TeacherModelAccess
        {
            TeacherUserId = "teacher",
            ModelKey = "model",
            IsEnabled = true,
            PeriodSeconds = periodSeconds,
            MaxChecks = 1,
            CreatedAt = now,
            UpdatedAt = now
        });
        dbContext.TeacherModelUsages.Add(new TeacherModelUsage
        {
            TeacherUserId = "teacher",
            ModelKey = "model",
            PeriodStart = TeacherModelAccessService.GetPeriodStart(now, periodSeconds),
            PeriodSeconds = periodSeconds,
            UsedChecks = 1,
            CreatedAt = now,
            UpdatedAt = now
        });
        await dbContext.SaveChangesAsync();
        var queue = new FakeReviewQueuePublisher();
        var handler = CreateEventHandler(dbContext, queue);
        var policy = CreatePolicy(
            new ReviewTaskPolicySnapshot(
                "llm",
                ReviewTaskType.FreeText,
                ReviewCheckMode.Llm,
                "Essay",
                "Explain.",
                5m,
                0,
                []),
            modelKey: "model");

        await handler.HandleAsync(policy, CancellationToken.None);
        await handler.HandleAsync(
            CreateAttempt() with { Answers = new Dictionary<string, string> { ["llm"] = "Answer" } },
            CancellationToken.None);

        var review = await dbContext.Reviews.Include(item => item.TaskResults).SingleAsync();
        Assert.Equal(ReviewStatus.Queued, review.Status);
        Assert.Equal(ReviewTaskStatus.Pending, review.TaskResults.Single().Status);
        Assert.Contains("Лимит проверок", review.LlmQuotaReservationError);
        Assert.Null(review.LlmQuotaReservedAt);
        Assert.Single(queue.Messages);
    }

    [Fact]
    public async Task DisabledLlmGateway_KeepsLlmTaskQueuedForLaterRetry()
    {
        await using var dbContext = CreateDbContext();
        var queue = new FakeReviewQueuePublisher();
        var handler = CreateEventHandler(dbContext, queue, llmGatewayEnabled: false);
        var policy = CreatePolicy(
            new ReviewTaskPolicySnapshot(
                "llm",
                ReviewTaskType.FreeText,
                ReviewCheckMode.Llm,
                "Essay",
                "Explain.",
                5m,
                0,
                []));

        await handler.HandleAsync(policy, CancellationToken.None);
        await handler.HandleAsync(
            CreateAttempt() with
            {
                Answers = new Dictionary<string, string> { ["llm"] = "Answer" }
            },
            CancellationToken.None);

        var review = await dbContext.Reviews.Include(item => item.TaskResults).SingleAsync();
        Assert.Equal(ReviewStatus.Queued, review.Status);
        Assert.Equal(ReviewTaskStatus.Pending, review.TaskResults.Single().Status);
        Assert.Contains("отключена", review.LlmQuotaReservationError);
        Assert.Null(review.LlmQuotaReservedAt);
        Assert.Single(queue.Messages);
    }

    [Fact]
    public async Task InvalidSingleChoicePolicy_IsRejectedBeforeInboxAcknowledgement()
    {
        await using var dbContext = CreateDbContext();
        var handler = CreateEventHandler(dbContext, new FakeReviewQueuePublisher());
        var invalidPolicy = CreatePolicy(
            new ReviewTaskPolicySnapshot(
                "choice",
                ReviewTaskType.SingleChoice,
                ReviewCheckMode.Auto,
                "Choice",
                "Choose.",
                2m,
                0,
                [
                    new ReviewAnswerOptionSnapshot("a", "A", false),
                    new ReviewAnswerOptionSnapshot("b", "B", false)
                ]));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            handler.HandleAsync(invalidPolicy, CancellationToken.None));

        Assert.Empty(dbContext.TestReviewPolicies);
        Assert.Empty(dbContext.InboxMessages);
    }

    private static ReviewIntegrationEventHandler CreateEventHandler(
        ReviewDbContext dbContext,
        FakeReviewQueuePublisher queue,
        bool llmGatewayEnabled = true)
    {
        var scoring = new ReviewScoringService();
        var modelAccess = new TeacherModelAccessService(
            dbContext,
            new FakeCatalogClient());
        var creation = new ReviewCreationService(
            dbContext,
            scoring,
            modelAccess,
            OptionsFactory.Create(new ReviewProcessingOptions { LlmGatewayEnabled = llmGatewayEnabled }));
        return new ReviewIntegrationEventHandler(
            dbContext,
            creation,
            queue,
            NullLogger<ReviewIntegrationEventHandler>.Instance);
    }

    private static ReviewDbContext CreateDbContext()
    {
        return new ReviewDbContext(
            new DbContextOptionsBuilder<ReviewDbContext>()
                .UseInMemoryDatabase($"review-service-tests-{Guid.NewGuid():N}")
                .Options);
    }

    private static TestReviewPolicyPublishedV1 CreatePolicy(
        ReviewTaskPolicySnapshot? task = null,
        string modelKey = "model")
    {
        task ??= new ReviewTaskPolicySnapshot(
            "choice",
            ReviewTaskType.SingleChoice,
            ReviewCheckMode.Auto,
            "Choice",
            "Choose.",
            2m,
            0,
            [
                new ReviewAnswerOptionSnapshot("a", "Correct", true),
                new ReviewAnswerOptionSnapshot("b", "Wrong", false)
            ]);
        return new TestReviewPolicyPublishedV1(
            Guid.NewGuid(),
            "test-1",
            1,
            "teacher",
            "Test",
            modelKey,
            [task],
            Now.AddMinutes(-10));
    }

    private static AttemptSubmittedV1 CreateAttempt()
    {
        return new AttemptSubmittedV1(
            Guid.NewGuid(),
            42,
            "test-1",
            1,
            "student",
            "Student",
            new Dictionary<string, string> { ["choice"] = "a" },
            Now);
    }

    private sealed class FakeReviewQueuePublisher : IReviewQueuePublisher
    {
        public List<ReviewQueueMessage> Messages { get; } = [];

        public Task PublishAsync(
            ReviewQueueMessage message,
            int? retryDelaySeconds,
            CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCatalogClient : ILlmGatewayModelCatalogClient
    {
        public Task<List<LlmModelCatalogItemResponse>> GetModelsAsync(
            CancellationToken cancellationToken)
        {
            List<LlmModelCatalogItemResponse> result =
            [
                new LlmModelCatalogItemResponse("model", "Model", true)
            ];
            return Task.FromResult(result);
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OptionsFactory = Microsoft.Extensions.Options.Options;
using ReviewService.Options;
using ReviewService.Contracts.Enums;
using ReviewService.Data;
using ReviewService.Enums;
using ReviewService.Interfaces;
using ReviewService.Messaging;
using ReviewService.Models.ModelAccess;
using ReviewService.Models.Reviews;
using ReviewService.Services;

namespace ReviewService.Tests;

public sealed class ReviewJobProcessorTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(2)]
    [InlineData(25)]
    [InlineData(int.MaxValue)]
    public async Task ExhaustedRetryDelays_KeepUsingFinalDelayWithoutManualFallback(
        int taskAttempts)
    {
        await using var dbContext = CreateDbContext();
        var review = CreateReview(taskAttempts);
        dbContext.Reviews.Add(review);
        await dbContext.SaveChangesAsync();
        var reviewId = review.Id;
        dbContext.ChangeTracker.Clear();
        var llmClient = new FailingLlmReviewClient(TimeSpan.Zero);
        var processor = CreateProcessor(
            dbContext,
            llmClient,
            retryDelaysSeconds: [30, 120]);

        var beforeProcess = DateTimeOffset.UtcNow;
        var outcome = await processor.ProcessAsync(
            new ReviewQueueMessage
            {
                ReviewId = reviewId,
                AttemptId = review.AttemptId,
                ModelKey = "model",
                RequestedAt = Now
            },
            CancellationToken.None);
        var afterProcess = DateTimeOffset.UtcNow;

        dbContext.ChangeTracker.Clear();
        var retryReview = await dbContext.Reviews
            .Include(item => item.TaskResults)
            .SingleAsync(item => item.Id == reviewId);
        var retryTask = Assert.Single(retryReview.TaskResults);
        Assert.Equal(ReviewProcessingOutcomeType.RetryScheduled, outcome.Type);
        Assert.Equal(120, outcome.RetryDelaySeconds);
        Assert.Equal(ReviewStatus.RetryScheduled, retryReview.Status);
        AssertTimestampBetween(
            retryReview.NextRetryAt,
            beforeProcess.AddSeconds(120),
            afterProcess.AddSeconds(120));
        Assert.Null(retryReview.ProcessingLeaseExpiresAt);
        Assert.Equal("Task llm: LLM failure.", retryReview.LastError);
        Assert.Equal(ReviewTaskStatus.RetryScheduled, retryTask.Status);
        Assert.Equal(
            taskAttempts == int.MaxValue ? int.MaxValue : taskAttempts + 1,
            retryTask.Attempts);
        AssertTimestampBetween(
            retryTask.NextRetryAt,
            beforeProcess.AddSeconds(120),
            afterProcess.AddSeconds(120));
        Assert.Equal("LLM failure.", retryTask.LastError);
        Assert.Equal(1, llmClient.CallCount);
    }

    [Fact]
    public async Task Retry_IsScheduledFromFailureTimeAfterLlmCall()
    {
        await using var dbContext = CreateDbContext();
        var review = CreateReview(taskAttempts: 0);
        dbContext.Reviews.Add(review);
        await dbContext.SaveChangesAsync();
        var reviewId = review.Id;
        dbContext.ChangeTracker.Clear();
        var llmDuration = TimeSpan.FromMilliseconds(50);
        var llmClient = new FailingLlmReviewClient(llmDuration);
        var processor = CreateProcessor(
            dbContext,
            llmClient,
            retryDelaysSeconds: [30, 120]);

        var outcome = await processor.ProcessAsync(
            new ReviewQueueMessage
            {
                ReviewId = reviewId,
                AttemptId = review.AttemptId,
                ModelKey = "model",
                RequestedAt = Now
            },
            CancellationToken.None);
        var afterProcess = DateTimeOffset.UtcNow;

        dbContext.ChangeTracker.Clear();
        var retryReview = await dbContext.Reviews
            .Include(item => item.TaskResults)
            .SingleAsync(item => item.Id == reviewId);
        var retryTask = Assert.Single(retryReview.TaskResults);
        Assert.Equal(ReviewProcessingOutcomeType.RetryScheduled, outcome.Type);
        Assert.Equal(30, outcome.RetryDelaySeconds);
        Assert.Equal(ReviewStatus.RetryScheduled, retryReview.Status);
        Assert.Null(retryReview.ProcessingLeaseExpiresAt);
        Assert.Equal(ReviewTaskStatus.RetryScheduled, retryTask.Status);
        Assert.Equal(1, retryTask.Attempts);
        Assert.NotNull(llmClient.CallStartedAt);
        Assert.NotNull(llmClient.FailedAt);
        AssertTimestampBetween(
            retryTask.NextRetryAt,
            llmClient.FailedAt.Value.AddSeconds(30),
            afterProcess.AddSeconds(30));
        AssertTimestampBetween(
            retryReview.NextRetryAt,
            retryTask.NextRetryAt!.Value,
            afterProcess.AddSeconds(30));
        Assert.True(
            retryTask.NextRetryAt > llmClient.CallStartedAt.Value.AddSeconds(30),
            "Retry must be scheduled from the failure time, after the LLM call has completed.");
        Assert.Equal(1, llmClient.CallCount);
    }

    [Fact]
    public async Task ProviderDisabledException_IsRetriedWithoutManualFallback()
    {
        await using var dbContext = CreateDbContext();
        var review = CreateReview(taskAttempts: 0);
        dbContext.Reviews.Add(review);
        await dbContext.SaveChangesAsync();
        var reviewId = review.Id;
        dbContext.ChangeTracker.Clear();
        var llmClient = new FailingLlmReviewClient(
            TimeSpan.Zero,
            new InvalidOperationException("Gateway disabled."));
        var processor = CreateProcessor(
            dbContext,
            llmClient,
            retryDelaysSeconds: [30]);

        var outcome = await processor.ProcessAsync(
            new ReviewQueueMessage
            {
                ReviewId = reviewId,
                AttemptId = review.AttemptId,
                ModelKey = "model",
                RequestedAt = Now
            },
            CancellationToken.None);

        var storedReview = await dbContext.Reviews
            .Include(item => item.TaskResults)
            .SingleAsync(item => item.Id == reviewId);
        Assert.Equal(ReviewProcessingOutcomeType.RetryScheduled, outcome.Type);
        Assert.Equal(ReviewStatus.RetryScheduled, storedReview.Status);
        Assert.Equal(ReviewTaskStatus.RetryScheduled, Assert.Single(storedReview.TaskResults).Status);
        Assert.Equal(1, llmClient.CallCount);
    }

    [Fact]
    public async Task DisabledGatewayConfiguration_IsRetriedWithoutConsumingQuota()
    {
        await using var dbContext = CreateDbContext();
        var review = CreateReview(taskAttempts: 0);
        review.LlmQuotaReservedAt = null;
        dbContext.Reviews.Add(review);
        await dbContext.SaveChangesAsync();
        var reviewId = review.Id;
        dbContext.ChangeTracker.Clear();
        var llmClient = new SuccessfulLlmReviewClient();
        var processor = CreateProcessor(
            dbContext,
            llmClient,
            retryDelaysSeconds: [30],
            llmGatewayEnabled: false);

        var outcome = await processor.ProcessAsync(
            new ReviewQueueMessage
            {
                ReviewId = reviewId,
                AttemptId = review.AttemptId,
                ModelKey = "model",
                RequestedAt = Now
            },
            CancellationToken.None);

        dbContext.ChangeTracker.Clear();
        var storedReview = await dbContext.Reviews
            .Include(item => item.TaskResults)
            .SingleAsync(item => item.Id == reviewId);
        Assert.Equal(ReviewProcessingOutcomeType.RetryScheduled, outcome.Type);
        Assert.Equal(ReviewStatus.RetryScheduled, storedReview.Status);
        Assert.Equal(ReviewTaskStatus.RetryScheduled, Assert.Single(storedReview.TaskResults).Status);
        Assert.Contains("отключена", storedReview.LlmQuotaReservationError);
        Assert.Null(storedReview.LlmQuotaReservedAt);
        Assert.Empty(dbContext.TeacherModelUsages);
        Assert.Equal(0, llmClient.CallCount);
    }

    [Fact]
    public async Task MissingModelAccess_IsRetriedWithoutCallingProvider()
    {
        await using var dbContext = CreateDbContext();
        var review = CreateReview(taskAttempts: 0);
        review.LlmQuotaReservedAt = null;
        dbContext.Reviews.Add(review);
        await dbContext.SaveChangesAsync();
        var reviewId = review.Id;
        dbContext.ChangeTracker.Clear();
        var llmClient = new SuccessfulLlmReviewClient();
        var processor = CreateProcessor(
            dbContext,
            llmClient,
            retryDelaysSeconds: [30]);

        var outcome = await processor.ProcessAsync(
            new ReviewQueueMessage
            {
                ReviewId = reviewId,
                AttemptId = review.AttemptId,
                ModelKey = "model",
                RequestedAt = Now
            },
            CancellationToken.None);

        dbContext.ChangeTracker.Clear();
        var storedReview = await dbContext.Reviews
            .Include(item => item.TaskResults)
            .SingleAsync(item => item.Id == reviewId);
        Assert.Equal(ReviewProcessingOutcomeType.RetryScheduled, outcome.Type);
        Assert.Equal(ReviewStatus.RetryScheduled, storedReview.Status);
        Assert.Equal(ReviewTaskStatus.RetryScheduled, Assert.Single(storedReview.TaskResults).Status);
        Assert.Contains("не выдан доступ", storedReview.LlmQuotaReservationError);
        Assert.Null(storedReview.LlmQuotaReservedAt);
        Assert.Empty(dbContext.TeacherModelUsages);
        Assert.Equal(0, llmClient.CallCount);
    }

    [Fact]
    public async Task AvailableQuota_IsReservedOnceBeforeCallingProvider()
    {
        await using var dbContext = CreateDbContext();
        var review = CreateReview(taskAttempts: 0);
        review.LlmQuotaReservedAt = null;
        dbContext.Reviews.Add(review);
        dbContext.TeacherModelAccesses.Add(new TeacherModelAccess
        {
            TeacherUserId = review.TeacherUserId,
            ModelKey = review.ModelKeySnapshot,
            IsEnabled = true,
            PeriodSeconds = 3600,
            MaxChecks = 1,
            CreatedAt = Now,
            UpdatedAt = Now
        });
        await dbContext.SaveChangesAsync();
        var reviewId = review.Id;
        dbContext.ChangeTracker.Clear();
        var llmClient = new SuccessfulLlmReviewClient();
        var processor = CreateProcessor(
            dbContext,
            llmClient,
            retryDelaysSeconds: [30]);

        var beforeProcess = DateTimeOffset.UtcNow;
        var outcome = await processor.ProcessAsync(
            new ReviewQueueMessage
            {
                ReviewId = reviewId,
                AttemptId = review.AttemptId,
                ModelKey = "model",
                RequestedAt = Now
            },
            CancellationToken.None);
        var afterProcess = DateTimeOffset.UtcNow;

        dbContext.ChangeTracker.Clear();
        var storedReview = await dbContext.Reviews
            .Include(item => item.TaskResults)
            .SingleAsync(item => item.Id == reviewId);
        Assert.Equal(ReviewProcessingOutcomeType.Completed, outcome.Type);
        Assert.Equal(ReviewStatus.Checked, storedReview.Status);
        Assert.Equal(ReviewTaskStatus.Succeeded, Assert.Single(storedReview.TaskResults).Status);
        AssertTimestampBetween(storedReview.LlmQuotaReservedAt, beforeProcess, afterProcess);
        Assert.Equal(string.Empty, storedReview.LlmQuotaReservationError);
        Assert.Equal(1, (await dbContext.TeacherModelUsages.SingleAsync()).UsedChecks);
        Assert.Equal(1, llmClient.CallCount);
    }

    [Fact]
    public async Task HttpClientTimeout_IsCountedAndScheduledForRetry()
    {
        await using var dbContext = CreateDbContext();
        var review = CreateReview(taskAttempts: 0);
        dbContext.Reviews.Add(review);
        await dbContext.SaveChangesAsync();
        var reviewId = review.Id;
        dbContext.ChangeTracker.Clear();
        var llmClient = new FailingLlmReviewClient(
            TimeSpan.Zero,
            new TaskCanceledException("The LLM request timed out."));
        var processor = CreateProcessor(
            dbContext,
            llmClient,
            retryDelaysSeconds: [30]);

        var beforeProcess = DateTimeOffset.UtcNow;
        var outcome = await processor.ProcessAsync(
            new ReviewQueueMessage
            {
                ReviewId = reviewId,
                AttemptId = review.AttemptId,
                ModelKey = "model",
                RequestedAt = Now
            },
            CancellationToken.None);
        var afterProcess = DateTimeOffset.UtcNow;

        dbContext.ChangeTracker.Clear();
        var retryReview = await dbContext.Reviews
            .Include(item => item.TaskResults)
            .SingleAsync(item => item.Id == reviewId);
        var retryTask = Assert.Single(retryReview.TaskResults);
        Assert.Equal(ReviewProcessingOutcomeType.RetryScheduled, outcome.Type);
        Assert.Equal(1, retryTask.Attempts);
        Assert.Equal(ReviewTaskStatus.RetryScheduled, retryTask.Status);
        AssertTimestampBetween(
            retryReview.NextRetryAt,
            beforeProcess.AddSeconds(30),
            afterProcess.AddSeconds(30));
    }

    private static ReviewJobProcessor CreateProcessor(
        ReviewDbContext dbContext,
        ILlmGatewayReviewClient llmClient,
        int[] retryDelaysSeconds,
        bool llmGatewayEnabled = true)
    {
        var rabbitMqOptions = new RabbitMqOptions
        {
            RetryDelaysSeconds = retryDelaysSeconds
        };
        return new ReviewJobProcessor(
            dbContext,
            new ReviewScoringService(),
            llmClient,
            new TeacherModelAccessService(
                dbContext,
                new FakeCatalogClient()),
            new RabbitMqTopology(OptionsFactory.Create(rabbitMqOptions)),
            OptionsFactory.Create(new ReviewProcessingOptions
            {
                ProcessingLeaseSeconds = 300,
                LlmGatewayEnabled = llmGatewayEnabled
            }),
            NullLogger<ReviewJobProcessor>.Instance);
    }

    private static void AssertTimestampBetween(
        DateTimeOffset? actual,
        DateTimeOffset inclusiveLowerBound,
        DateTimeOffset inclusiveUpperBound)
    {
        Assert.NotNull(actual);
        Assert.InRange(actual.Value, inclusiveLowerBound, inclusiveUpperBound);
    }

    private static ReviewDbContext CreateDbContext()
    {
        return new ReviewDbContext(
            new DbContextOptionsBuilder<ReviewDbContext>()
                .UseInMemoryDatabase($"review-job-tests-{Guid.NewGuid():N}")
                .Options);
    }

    private static Review CreateReview(int taskAttempts)
    {
        return new Review
        {
            AttemptId = Guid.NewGuid(),
            TestId = "test-1",
            TestRevision = 1,
            TestTitle = "Test",
            TeacherUserId = "teacher",
            StudentUserId = "student",
            Status = taskAttempts == 0
                ? ReviewStatus.Queued
                : ReviewStatus.RetryScheduled,
            ModelKeySnapshot = "model",
            LlmQuotaReservedAt = Now.AddMinutes(-5),
            SubmittedAt = Now.AddMinutes(-5),
            QueuedAt = Now.AddMinutes(-4),
            NextRetryAt = taskAttempts == 0 ? null : Now.AddSeconds(-1),
            MaxScore = 5m,
            TaskResults =
            [
                new ReviewTask
                {
                    TaskId = "llm",
                    TaskType = ReviewTaskType.FreeText,
                    TaskTitle = "Essay",
                    TaskPrompt = "Explain.",
                    StudentAnswer = "An answer.",
                    CheckMode = ReviewCheckMode.Llm,
                    Status = taskAttempts == 0
                        ? ReviewTaskStatus.Pending
                        : ReviewTaskStatus.RetryScheduled,
                    Attempts = taskAttempts,
                    NextRetryAt = taskAttempts == 0 ? null : Now.AddSeconds(-1),
                    MaxScore = 5m
                }
            ]
        };
    }

    private sealed class SuccessfulLlmReviewClient : ILlmGatewayReviewClient
    {
        public int CallCount { get; private set; }

        public Task<LlmTaskReviewResult> ReviewFreeTextAnswerAsync(
            string modelKey,
            ReviewTask task,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new LlmTaskReviewResult(
                task.MaxScore,
                "Checked.",
                ["No issues."]));
        }
    }

    private sealed class FakeCatalogClient : ILlmGatewayModelCatalogClient
    {
        public Task<List<ReviewService.Contracts.Responses.LlmModelCatalogItemResponse>> GetModelsAsync(
            CancellationToken cancellationToken)
        {
            List<ReviewService.Contracts.Responses.LlmModelCatalogItemResponse> result = [];
            return Task.FromResult(result);
        }
    }

    private sealed class FailingLlmReviewClient(
        TimeSpan delay,
        Exception? exception = null) : ILlmGatewayReviewClient
    {
        public int CallCount { get; private set; }
        public DateTimeOffset? CallStartedAt { get; private set; }
        public DateTimeOffset? FailedAt { get; private set; }

        public async Task<LlmTaskReviewResult> ReviewFreeTextAnswerAsync(
            string modelKey,
            ReviewTask task,
            CancellationToken cancellationToken)
        {
            CallCount++;
            CallStartedAt = DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, cancellationToken);

            FailedAt = DateTimeOffset.UtcNow;
            throw exception ?? new InvalidOperationException("LLM failure.");
        }
    }
}

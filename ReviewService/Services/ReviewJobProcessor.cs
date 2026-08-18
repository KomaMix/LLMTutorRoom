using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReviewService.Options;
using ReviewService.Contracts.Enums;
using ReviewService.Data;
using ReviewService.Enums;
using ReviewService.Interfaces;
using ReviewService.Messaging;
using ReviewService.Models.Reviews;

namespace ReviewService.Services;

public sealed class ReviewJobProcessor(
    ReviewDbContext dbContext,
    IReviewScoringService scoringService,
    ILlmGatewayReviewClient llmGatewayReviewClient,
    ITeacherModelAccessService modelAccessService,
    RabbitMqTopology topology,
    IOptions<ReviewProcessingOptions> processingOptions,
    ILogger<ReviewJobProcessor> logger) : IReviewJobProcessor
{
    private readonly ReviewProcessingOptions _processingOptions = processingOptions.Value;

    public async Task<ReviewProcessingOutcome> ProcessAsync(
        ReviewQueueMessage message,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var storedAttemptId = await dbContext.Reviews
            .AsNoTracking()
            .Where(item => item.Id == message.ReviewId)
            .Select(item => (int?)item.AttemptId)
            .SingleOrDefaultAsync(cancellationToken);
        if (!storedAttemptId.HasValue)
        {
            logger.LogWarning("Review {ReviewId} was not found.", message.ReviewId);
            return ReviewProcessingOutcome.Poison();
        }

        if (storedAttemptId.Value != message.AttemptId)
        {
            logger.LogWarning(
                "Review {ReviewId} belongs to attempt {ActualAttemptId}, not {MessageAttemptId}.",
                message.ReviewId,
                storedAttemptId.Value,
                message.AttemptId);
            return ReviewProcessingOutcome.Poison();
        }

        var review = await TryClaimReviewAsync(message.ReviewId, now, cancellationToken);
        if (review is null)
            return ReviewProcessingOutcome.Ignored();

        var retryableLlmTasks = review.TaskResults
            .Where(task => task.CheckMode == ReviewCheckMode.Llm
                && task.Status is not ReviewTaskStatus.Succeeded
                && task.Status is not ReviewTaskStatus.ManualReview)
            .ToList();
        var readinessError = await EnsureLlmExecutionReadyAsync(
            review,
            retryableLlmTasks.Count,
            cancellationToken);
        if (readinessError is not null)
        {
            var retryAt = DateTimeOffset.UtcNow;
            var readinessDelay = 0;
            foreach (var task in retryableLlmTasks)
            {
                readinessDelay = Math.Max(
                    readinessDelay,
                    MarkTaskForRetry(task, readinessError, retryAt));
            }

            review.Status = ReviewStatus.RetryScheduled;
            review.CompletedAt = null;
            review.LastError = CreateReviewErrorSummary(review);
            review.NextRetryAt = retryAt.AddSeconds(readinessDelay);
            review.ProcessingLeaseExpiresAt = null;
            await dbContext.SaveChangesAsync(cancellationToken);
            return ReviewProcessingOutcome.RetryScheduled(readinessDelay);
        }

        var retryDelaySeconds = 0;
        foreach (var task in review.TaskResults.Where(task => task.CheckMode == ReviewCheckMode.Llm))
        {
            if (task.Status is ReviewTaskStatus.Succeeded or ReviewTaskStatus.ManualReview)
                continue;

            review.ProcessingLeaseExpiresAt = DateTimeOffset.UtcNow
                .AddSeconds(_processingOptions.ProcessingLeaseSeconds);
            task.Status = ReviewTaskStatus.Processing;
            await dbContext.SaveChangesAsync(cancellationToken);
            try
            {
                var llmResult = await llmGatewayReviewClient.ReviewFreeTextAnswerAsync(
                    review.ModelKeySnapshot,
                    task,
                    cancellationToken);
                scoringService.ApplyLlmResult(task, llmResult, DateTimeOffset.UtcNow);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "LLM check failed for review {ReviewId}, task {TaskId}.",
                    review.Id,
                    task.TaskId);
                var failureTime = DateTimeOffset.UtcNow;
                var delaySeconds = MarkTaskForRetry(task, exception.Message, failureTime);
                retryDelaySeconds = Math.Max(retryDelaySeconds, delaySeconds);
            }
        }

        scoringService.RecalculateReview(review);
        if (retryDelaySeconds > 0)
        {
            review.Status = ReviewStatus.RetryScheduled;
            review.LastError = CreateReviewErrorSummary(review);
            review.NextRetryAt = DateTimeOffset.UtcNow.AddSeconds(retryDelaySeconds);
            review.ProcessingLeaseExpiresAt = null;
            await dbContext.SaveChangesAsync(cancellationToken);
            return ReviewProcessingOutcome.RetryScheduled(retryDelaySeconds);
        }

        review.Status = scoringService.GetReviewStatusAfterTaskProcessing(review);
        review.CompletedAt = review.Status == ReviewStatus.Checked
            ? DateTimeOffset.UtcNow
            : null;
        review.ProcessingLeaseExpiresAt = null;
        review.NextRetryAt = null;
        review.LastError = string.Empty;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ReviewProcessingOutcome.Completed();
    }

    private async Task<Review?> TryClaimReviewAsync(
        int reviewId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var leaseExpiresAt = now.AddSeconds(_processingOptions.ProcessingLeaseSeconds);
        if (dbContext.Database.IsRelational())
        {
            var claimed = await dbContext.Reviews
                .Where(review => review.Id == reviewId
                    && review.Status != ReviewStatus.Checked
                    && review.Status != ReviewStatus.ManualReview
                    && review.Status != ReviewStatus.Failed
                    && (review.Status != ReviewStatus.Processing
                        || !review.ProcessingLeaseExpiresAt.HasValue
                        || review.ProcessingLeaseExpiresAt <= now)
                    && (review.Status != ReviewStatus.RetryScheduled
                        || !review.NextRetryAt.HasValue
                        || review.NextRetryAt <= now))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(review => review.Status, ReviewStatus.Processing)
                    .SetProperty(review => review.StartedAt, review => review.StartedAt ?? now)
                    .SetProperty(
                        review => review.ProcessingAttempts,
                        review => review.ProcessingAttempts < int.MaxValue
                            ? review.ProcessingAttempts + 1
                            : int.MaxValue)
                    .SetProperty(
                        review => review.ProcessingGeneration,
                        review => review.ProcessingGeneration + 1)
                    .SetProperty(review => review.ProcessingLeaseExpiresAt, leaseExpiresAt)
                    .SetProperty(review => review.NextRetryAt, (DateTimeOffset?)null),
                    cancellationToken);
            if (claimed == 0)
                return null;

            return await dbContext.Reviews
                .Include(item => item.TaskResults)
                .SingleAsync(item => item.Id == reviewId, cancellationToken);
        }

        var inMemoryReview = await dbContext.Reviews
            .Include(item => item.TaskResults)
            .SingleAsync(item => item.Id == reviewId, cancellationToken);
        if (!ShouldProcess(inMemoryReview, now))
            return null;

        inMemoryReview.Status = ReviewStatus.Processing;
        inMemoryReview.StartedAt ??= now;
        if (inMemoryReview.ProcessingAttempts < int.MaxValue)
            inMemoryReview.ProcessingAttempts++;
        inMemoryReview.ProcessingGeneration++;
        inMemoryReview.ProcessingLeaseExpiresAt = leaseExpiresAt;
        inMemoryReview.NextRetryAt = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return inMemoryReview;
    }

    private static bool ShouldProcess(Review review, DateTimeOffset now)
    {
        if (review.Status is ReviewStatus.Checked or ReviewStatus.ManualReview or ReviewStatus.Failed)
            return false;
        if (review.Status == ReviewStatus.Processing && review.ProcessingLeaseExpiresAt > now)
            return false;
        if (review.Status == ReviewStatus.RetryScheduled && review.NextRetryAt > now)
            return false;
        return true;
    }

    private int MarkTaskForRetry(
        ReviewTask task,
        string error,
        DateTimeOffset now)
    {
        task.Attempts = task.Attempts switch
        {
            < 0 => 1,
            < int.MaxValue => task.Attempts + 1,
            _ => int.MaxValue
        };
        task.LastError = error;

        var delaySeconds = topology.GetRetryDelaySeconds(task.Attempts);
        task.Status = ReviewTaskStatus.RetryScheduled;
        task.NextRetryAt = now.AddSeconds(delaySeconds);
        return delaySeconds;
    }

    private async Task<string?> EnsureLlmExecutionReadyAsync(
        Review review,
        int llmTaskCount,
        CancellationToken cancellationToken)
    {
        if (llmTaskCount == 0)
            return null;

        if (!_processingOptions.LlmGatewayEnabled)
        {
            const string error = "Автоматическая LLM-проверка временно отключена в ReviewService.";
            review.LlmQuotaReservationError = error;
            return error;
        }

        if (string.IsNullOrWhiteSpace(review.TeacherUserId))
        {
            const string error = "Для теста не указан преподаватель-владелец.";
            review.LlmQuotaReservationError = error;
            return error;
        }

        if (string.IsNullOrWhiteSpace(review.ModelKeySnapshot))
        {
            const string error = "Для теста не выбрана LLM-модель проверки.";
            review.LlmQuotaReservationError = error;
            return error;
        }

        if (review.LlmQuotaReservedAt.HasValue)
        {
            review.LlmQuotaReservationError = string.Empty;
            return null;
        }

        await using var transaction = dbContext.Database.IsRelational()
            && dbContext.Database.CurrentTransaction is null
                ? await dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken)
                : null;
        var reservedAt = DateTimeOffset.UtcNow;
        review.LlmQuotaReservedAt = reservedAt;
        review.LlmQuotaReservationError = string.Empty;

        var quota = await modelAccessService.TryConsumeChecksAsync(
            review.TeacherUserId,
            review.ModelKeySnapshot,
            llmTaskCount,
            cancellationToken);
        if (quota.Status == ModelQuotaConsumptionStatus.Allowed)
        {
            // Keep the quota debit and its durable reservation marker indivisible. The quota
            // service uses this same DbContext/transaction, so a crash can never debit usage
            // without preventing a second debit on the next worker pass.
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
            return null;
        }

        review.LlmQuotaReservedAt = null;
        var quotaError = ReviewCreationService.CreateQuotaError(
            review.ModelKeySnapshot,
            quota);
        review.LlmQuotaReservationError = quotaError;
        return quotaError;
    }

    private static string CreateReviewErrorSummary(Review review)
    {
        var failedTask = review.TaskResults
            .Where(task => task.Status == ReviewTaskStatus.RetryScheduled)
            .OrderByDescending(task => task.NextRetryAt)
            .FirstOrDefault();
        return failedTask is null
            ? string.Empty
            : $"Task {failedTask.TaskId}: {failedTask.LastError}";
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReviewService.Options;
using ReviewService.Contracts.Enums;
using ReviewService.Data;
using ReviewService.Interfaces;
using ReviewService.Models.Reviews;

namespace ReviewService.Messaging.ReviewProcessing;

public sealed class ReviewQueueMaintenanceService(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> rabbitMqOptions,
    IOptions<ReviewProcessingOptions> processingOptions,
    ILogger<ReviewQueueMaintenanceService> logger) : BackgroundService
{
    private readonly RabbitMqOptions _rabbitMqOptions = rabbitMqOptions.Value;
    private readonly ReviewProcessingOptions _processingOptions = processingOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_rabbitMqOptions.Enabled || !_processingOptions.WorkerEnabled)
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishDueReviewsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Review queue maintenance pass failed.");
            }

            await Task.Delay(
                TimeSpan.FromSeconds(Math.Max(1, _processingOptions.QueueMaintenanceIntervalSeconds)),
                stoppingToken);
        }
    }

    private async Task PublishDueReviewsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReviewDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IReviewQueuePublisher>();
        var now = DateTimeOffset.UtcNow;
        var enqueueCutoff = now.AddSeconds(-_processingOptions.EnqueueThrottleSeconds);

        if (dbContext.Database.IsRelational())
        {
            await dbContext.Reviews
                .Where(review => review.Status == ReviewStatus.Processing
                    && (!review.ProcessingLeaseExpiresAt.HasValue
                        || review.ProcessingLeaseExpiresAt <= now))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(review => review.Status, ReviewStatus.RetryScheduled)
                    .SetProperty(review => review.NextRetryAt, now)
                    .SetProperty(review => review.ProcessingLeaseExpiresAt, (DateTimeOffset?)null)
                    .SetProperty(review => review.LastError, "Processing lease expired.")
                    .SetProperty(
                        review => review.ProcessingGeneration,
                        review => review.ProcessingGeneration + 1),
                    cancellationToken);
        }
        else
        {
            var staleReviews = await dbContext.Reviews
                .Where(review => review.Status == ReviewStatus.Processing
                    && (!review.ProcessingLeaseExpiresAt.HasValue
                        || review.ProcessingLeaseExpiresAt <= now))
                .ToListAsync(cancellationToken);
            foreach (var review in staleReviews)
            {
                review.Status = ReviewStatus.RetryScheduled;
                review.NextRetryAt = now;
                review.ProcessingLeaseExpiresAt = null;
                review.LastError = "Processing lease expired.";
                review.ProcessingGeneration++;
            }

            if (staleReviews.Count > 0)
                await dbContext.SaveChangesAsync(cancellationToken);
        }

        var dueReviews = await dbContext.Reviews
            .AsNoTracking()
            .Where(review => (review.Status == ReviewStatus.Queued
                    || review.Status == ReviewStatus.RetryScheduled)
                && (!review.NextRetryAt.HasValue || review.NextRetryAt <= now)
                && (!review.LastEnqueuedAt.HasValue || review.LastEnqueuedAt <= enqueueCutoff))
            .OrderBy(review => review.SubmittedAt)
            .Take(25)
            .ToListAsync(cancellationToken);
        foreach (var review in dueReviews)
        {
            try
            {
                await publisher.PublishAsync(
                    new ReviewQueueMessage(
                        review.Id,
                        review.AttemptId,
                        review.ModelKeySnapshot,
                        now),
                    retryDelaySeconds: null,
                    cancellationToken);
                if (dbContext.Database.IsRelational())
                {
                    await dbContext.Reviews
                        .Where(item => item.Id == review.Id)
                        .ExecuteUpdateAsync(
                            setters => setters.SetProperty(
                                item => item.LastEnqueuedAt,
                                now),
                            cancellationToken);
                }
                else
                {
                    var storedReview = await dbContext.Reviews.FindAsync(
                        [review.Id],
                        cancellationToken);
                    if (storedReview is not null)
                    {
                        storedReview.LastEnqueuedAt = now;
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(
                    exception,
                    "Could not publish due review {ReviewId}.",
                    review.Id);
            }
        }
    }
}

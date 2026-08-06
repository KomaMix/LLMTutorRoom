using LLMTutorRoom.Data;
using LLMTutorRoom.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LLMTutorRoom.Services.ReviewProcessing
{
    public sealed class ReviewQueueMaintenanceService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly RabbitMqOptions _rabbitMqOptions;
        private readonly ReviewProcessingOptions _processingOptions;
        private readonly ILogger<ReviewQueueMaintenanceService> _logger;

        public ReviewQueueMaintenanceService(
            IServiceScopeFactory scopeFactory,
            IOptions<RabbitMqOptions> rabbitMqOptions,
            IOptions<ReviewProcessingOptions> processingOptions,
            ILogger<ReviewQueueMaintenanceService> logger)
        {
            _scopeFactory = scopeFactory;
            _rabbitMqOptions = rabbitMqOptions.Value;
            _processingOptions = processingOptions.Value;
            _logger = logger;
        }

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
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Review queue maintenance pass failed.");
                }

                await Task.Delay(
                    TimeSpan.FromSeconds(_processingOptions.QueueMaintenanceIntervalSeconds),
                    stoppingToken);
            }
        }

        private async Task PublishDueReviewsAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TutorRoomDbContext>();
            var publisher = scope.ServiceProvider.GetRequiredService<IReviewQueuePublisher>();
            var now = DateTimeOffset.UtcNow;
            var enqueueCutoff = now.AddSeconds(-_processingOptions.EnqueueThrottleSeconds);

            var staleProcessingReviews = await dbContext.SubmissionReviews
                .Where(review => review.Status == SubmissionReviewStatus.Processing
                    && review.ProcessingLeaseExpiresAt.HasValue
                    && review.ProcessingLeaseExpiresAt <= now)
                .ToListAsync(cancellationToken);

            foreach (var review in staleProcessingReviews)
            {
                review.Status = SubmissionReviewStatus.RetryScheduled;
                review.NextRetryAt = now;
                review.ProcessingLeaseExpiresAt = null;
                review.LastError = "Processing lease expired.";
            }

            if (staleProcessingReviews.Count > 0)
                await dbContext.SaveChangesAsync(cancellationToken);

            var dueReviews = await dbContext.SubmissionReviews
                .Where(review => review.AttemptId.HasValue
                    && (review.Status == SubmissionReviewStatus.Queued
                        || review.Status == SubmissionReviewStatus.RetryScheduled)
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
                        new ReviewQueueMessage
                        {
                            ReviewId = review.Id,
                            AttemptId = review.AttemptId!.Value,
                            ModelKey = review.ModelKey,
                            RequestedAt = now
                        },
                        retryDelaySeconds: null,
                        cancellationToken);

                    review.LastEnqueuedAt = now;
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(
                        ex,
                        "Could not publish due review {ReviewId} to RabbitMQ.",
                        review.Id);
                }
            }
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TeachingService.Data;
using TeachingService.Interfaces;
using TeachingService.Options;

namespace TeachingService.Services.IntegrationEvents
{
    public sealed class IntegrationOutboxPublisherService : BackgroundService
    {
        private readonly IntegrationEventOptions _options;
        private readonly IDbContextFactory<TeachingDbContext> _dbContextFactory;
        private readonly IIntegrationEventPublisher _publisher;
        private readonly ILogger<IntegrationOutboxPublisherService> _logger;

        public IntegrationOutboxPublisherService(
            IOptions<IntegrationEventOptions> options,
            IDbContextFactory<TeachingDbContext> dbContextFactory,
            IIntegrationEventPublisher publisher,
            ILogger<IntegrationOutboxPublisherService> logger)
        {
            _options = options.Value;
            _dbContextFactory = dbContextFactory;
            _publisher = publisher;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
            {
                _logger.LogInformation("Integration event publisher is disabled.");
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var foundMessages = await PublishDueBatchAsync(stoppingToken);
                    if (foundMessages)
                        continue;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogError(
                        exception,
                        "Unexpected failure while processing the integration outbox.");
                }

                await Task.Delay(
                    TimeSpan.FromMilliseconds(_options.PollIntervalMilliseconds),
                    stoppingToken);
            }
        }

        private async Task<bool> PublishDueBatchAsync(CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            Guid[] messageIds;

            await using (var dbContext = await _dbContextFactory.CreateDbContextAsync(
                cancellationToken))
            {
                messageIds = await dbContext.IntegrationOutboxMessages
                    .AsNoTracking()
                    .Where(message => message.PublishedAt == null
                        && message.NextPublishAttemptAt <= now)
                    .OrderBy(message => message.OccurredAt)
                    .Select(message => message.Id)
                    .Take(_options.BatchSize)
                    .ToArrayAsync(cancellationToken);
            }

            foreach (var messageId in messageIds)
                await PublishOneAsync(messageId, cancellationToken);

            return messageIds.Length > 0;
        }

        private async Task PublishOneAsync(
            Guid messageId,
            CancellationToken cancellationToken)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(
                cancellationToken);
            var message = await dbContext.IntegrationOutboxMessages
                .SingleOrDefaultAsync(item => item.Id == messageId, cancellationToken);

            if (message is null || message.PublishedAt.HasValue)
                return;

            try
            {
                await _publisher.PublishAsync(message, cancellationToken);
                message.MarkPublished(DateTimeOffset.UtcNow);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                var nextAttemptAt = DateTimeOffset.UtcNow.Add(GetRetryDelay(message.PublishAttempts));
                message.MarkPublishFailed(nextAttemptAt, GetStoredError(exception));
                await dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogWarning(
                    exception,
                    "Could not publish integration event {EventId}; next attempt is at {NextAttemptAt}.",
                    message.Id,
                    nextAttemptAt);
            }
        }

        private TimeSpan GetRetryDelay(int completedAttempts)
        {
            var exponent = Math.Min(completedAttempts, 10);
            var seconds = _options.RetryBaseDelaySeconds * Math.Pow(2, exponent);
            return TimeSpan.FromSeconds(Math.Min(seconds, _options.RetryMaxDelaySeconds));
        }

        private static string GetStoredError(Exception exception)
        {
            var error = $"{exception.GetType().Name}: {exception.Message}";
            return error.Length <= 2000
                ? error
                : error[..2000];
        }
    }
}

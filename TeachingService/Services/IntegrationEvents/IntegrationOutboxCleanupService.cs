using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TeachingService.Data;
using TeachingService.Options;

namespace TeachingService.Services.IntegrationEvents
{
    public sealed class IntegrationOutboxCleanupService : BackgroundService
    {
        private readonly IntegrationEventOptions _options;
        private readonly IDbContextFactory<TeachingDbContext> _dbContextFactory;
        private readonly ILogger<IntegrationOutboxCleanupService> _logger;

        public IntegrationOutboxCleanupService(
            IOptions<IntegrationEventOptions> options,
            IDbContextFactory<TeachingDbContext> dbContextFactory,
            ILogger<IntegrationOutboxCleanupService> logger)
        {
            _options = options.Value;
            _dbContextFactory = dbContextFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
                return;

            using var timer = new PeriodicTimer(
                TimeSpan.FromMinutes(_options.CleanupIntervalMinutes));

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await DeleteExpiredMessagesAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogError(
                        exception,
                        "Could not clean delivered integration outbox messages.");
                }

                try
                {
                    if (!await timer.WaitForNextTickAsync(stoppingToken))
                        break;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        private async Task<int> DeleteExpiredMessagesAsync(
            CancellationToken cancellationToken)
        {
            var cutoff = DateTimeOffset.UtcNow.AddDays(-_options.PublishedMessageRetentionDays);
            var totalDeleted = await DrainBatchesAsync(
                token => DeleteExpiredBatchAsync(cutoff, token),
                cancellationToken);

            if (totalDeleted == 0)
                return 0;

            _logger.LogInformation(
                "Deleted {MessageCount} delivered integration outbox messages older than {Cutoff}.",
                totalDeleted,
                cutoff);

            return totalDeleted;
        }

        private async Task<(int Selected, int Deleted)> DeleteExpiredBatchAsync(
            DateTimeOffset cutoff,
            CancellationToken cancellationToken)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(
                cancellationToken);
            var messageIds = await dbContext.IntegrationOutboxMessages
                .AsNoTracking()
                .Where(message => message.PublishedAt != null
                    && message.PublishedAt < cutoff)
                .OrderBy(message => message.PublishedAt)
                .Select(message => message.Id)
                .Take(_options.CleanupBatchSize)
                .ToArrayAsync(cancellationToken);

            if (messageIds.Length == 0)
                return (0, 0);

            var deleted = await dbContext.IntegrationOutboxMessages
                .Where(message => messageIds.Contains(message.Id))
                .ExecuteDeleteAsync(cancellationToken);
            return (messageIds.Length, deleted);
        }

        public static async Task<int> DrainBatchesAsync(
            Func<CancellationToken, Task<(int Selected, int Deleted)>> deleteBatchAsync,
            CancellationToken cancellationToken)
        {
            var totalDeleted = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var batch = await deleteBatchAsync(cancellationToken);
                totalDeleted += batch.Deleted;

                if (batch.Selected == 0 || batch.Deleted == 0)
                    return totalDeleted;
            }
        }
    }
}

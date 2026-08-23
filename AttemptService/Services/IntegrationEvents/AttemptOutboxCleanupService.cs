using AttemptService.Data;
using AttemptService.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AttemptService.Services.IntegrationEvents;

public sealed class AttemptOutboxCleanupService(
    IDbContextFactory<AttemptDbContext> dbContextFactory,
    IOptions<AttemptOutboxOptions> options,
    ILogger<AttemptOutboxCleanupService> logger) : BackgroundService
{
    private readonly AttemptOutboxOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DeleteExpiredMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Could not clean delivered attempt submission outbox messages.");
            }

            await Task.Delay(
                TimeSpan.FromMinutes(_options.CleanupIntervalMinutes),
                stoppingToken);
        }
    }

    public async Task<int> DeleteExpiredMessagesAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-_options.PublishedMessageRetentionDays);
        var totalDeleted = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var messageIds = await dbContext.AttemptSubmissionOutboxMessages
                .AsNoTracking()
                .Where(message => message.PublishedAt.HasValue
                    && message.PublishedAt < cutoff)
                .OrderBy(message => message.PublishedAt)
                .Select(message => message.Id)
                .Take(_options.CleanupBatchSize)
                .ToListAsync(cancellationToken);
            if (messageIds.Count == 0)
                break;

            var deleted = await dbContext.AttemptSubmissionOutboxMessages
                .Where(message => messageIds.Contains(message.Id))
                .ExecuteDeleteAsync(cancellationToken);
            totalDeleted += deleted;

            if (messageIds.Count < _options.CleanupBatchSize)
                break;
        }

        if (totalDeleted > 0)
        {
            logger.LogInformation(
                "Deleted {MessageCount} delivered attempt submission messages older than {Cutoff}.",
                totalDeleted,
                cutoff);
        }

        return totalDeleted;
    }
}

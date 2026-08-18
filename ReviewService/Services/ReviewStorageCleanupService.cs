using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReviewService.Options;
using ReviewService.Data;

namespace ReviewService.Services;

public sealed class ReviewStorageCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<ReviewStorageOptions> options,
    ILogger<ReviewStorageCleanupService> logger) : BackgroundService
{
    private readonly ReviewStorageOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Review storage cleanup pass failed.");
            }

            await Task.Delay(
                TimeSpan.FromMinutes(_options.CleanupIntervalMinutes),
                stoppingToken);
        }
    }

    internal async Task RunCleanupAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReviewDbContext>();
        var now = DateTimeOffset.UtcNow;
        var inboxCutoff = now.AddDays(-_options.InboxRetentionDays);
        var removedCount = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var eventIds = await dbContext.InboxMessages
                .AsNoTracking()
                .Where(message => message.ProcessedAt < inboxCutoff)
                .OrderBy(message => message.ProcessedAt)
                .Select(message => message.EventId)
                .Take(_options.CleanupBatchSize)
                .ToArrayAsync(cancellationToken);
            if (eventIds.Length == 0)
                break;

            removedCount += await dbContext.InboxMessages
                .Where(message => eventIds.Contains(message.EventId))
                .ExecuteDeleteAsync(cancellationToken);
            if (eventIds.Length < _options.CleanupBatchSize)
                break;
        }

        if (removedCount > 0)
        {
            logger.LogInformation(
                "Removed {InboxMessageCount} processed inbox messages older than {InboxCutoff}.",
                removedCount,
                inboxCutoff);
        }

        var pendingWarningCutoff = now.AddHours(-_options.PendingSubmissionWarningHours);
        var stalePending = await dbContext.PendingSubmissions
            .AsNoTracking()
            .Where(submission => submission.ReceivedAt < pendingWarningCutoff)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Count = group.Count(),
                OldestReceivedAt = group.Min(submission => submission.ReceivedAt)
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (stalePending is not null)
        {
            // Pending answers are intentionally retained: deleting them would make a late
            // policy event impossible to recover. This warning is the operator signal to
            // inspect the integration DLQ and republish the exact policy revision.
            logger.LogError(
                "{PendingSubmissionCount} submissions have waited for a review policy since at least {OldestReceivedAt}. Inspect the integration DLQ; pending answers were retained.",
                stalePending.Count,
                stalePending.OldestReceivedAt);
        }
    }
}

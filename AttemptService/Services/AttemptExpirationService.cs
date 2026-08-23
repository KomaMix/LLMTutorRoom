using AttemptService.Contracts.Enums;
using AttemptService.Data;
using AttemptService.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AttemptService.Services;

public sealed class AttemptExpirationService(
    IDbContextFactory<AttemptDbContext> dbContextFactory,
    IOptions<AttemptExpirationOptions> options,
    ILogger<AttemptExpirationService> logger) : BackgroundService
{
    private readonly AttemptExpirationOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Attempt expiration service is disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExpireDueAttemptsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Could not expire due attempts.");
            }

            await Task.Delay(
                TimeSpan.FromSeconds(_options.PollIntervalSeconds),
                stoppingToken);
        }
    }

    public async Task<int> ExpireDueAttemptsAsync(CancellationToken cancellationToken)
    {
        var expiredCount = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var now = DateTimeOffset.UtcNow;
            var attempts = await dbContext.TestAttempts
                .Where(attempt => attempt.Status == TestAttemptStatus.InProgress
                    && attempt.EndsAt <= now)
                .OrderBy(attempt => attempt.EndsAt)
                .Take(_options.BatchSize)
                .ToListAsync(cancellationToken);
            if (attempts.Count == 0)
                break;

            foreach (var attempt in attempts)
            {
                attempt.Status = TestAttemptStatus.Expired;
                attempt.StateRevision = checked(attempt.StateRevision + 1);
            }

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                expiredCount += attempts.Count;
            }
            catch (DbUpdateConcurrencyException)
            {
                continue;
            }

            if (attempts.Count < _options.BatchSize)
                break;
        }

        if (expiredCount > 0)
            logger.LogInformation("Expired {AttemptCount} due attempts.", expiredCount);

        return expiredCount;
    }
}

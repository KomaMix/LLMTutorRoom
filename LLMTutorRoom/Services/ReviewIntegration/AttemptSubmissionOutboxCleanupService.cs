using LLMTutorRoom.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LLMTutorRoom.Services.ReviewIntegration;

public sealed class AttemptSubmissionOutboxCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AttemptSubmissionOutboxOptions _options;
    private readonly ILogger<AttemptSubmissionOutboxCleanupService> _logger;

    public AttemptSubmissionOutboxCleanupService(
        IServiceScopeFactory scopeFactory,
        IOptions<AttemptSubmissionOutboxOptions> options,
        ILogger<AttemptSubmissionOutboxCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
            return;

        using var timer = new PeriodicTimer(
            TimeSpan.FromMinutes(_options.CleanupIntervalMinutes));

        do
        {
            try
            {
                await DeleteExpiredBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Could not clean delivered attempt submission outbox messages.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task DeleteExpiredBatchAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-_options.PublishedMessageRetentionDays);
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TutorRoomDbContext>();
        var messageIds = await dbContext.AttemptSubmissionOutboxMessages
            .AsNoTracking()
            .Where(message => message.PublishedAt.HasValue
                && message.PublishedAt < cutoff)
            .OrderBy(message => message.PublishedAt)
            .Select(message => message.Id)
            .Take(_options.CleanupBatchSize)
            .ToArrayAsync(cancellationToken);

        if (messageIds.Length == 0)
            return;

        var deleted = await dbContext.AttemptSubmissionOutboxMessages
            .Where(message => messageIds.Contains(message.Id))
            .ExecuteDeleteAsync(cancellationToken);
        _logger.LogInformation(
            "Deleted {MessageCount} delivered attempt submission messages older than {Cutoff}.",
            deleted,
            cutoff);
    }
}

using AttemptService.Contracts.Enums;
using AttemptService.Data;
using AttemptService.Options;
using AttemptService.Services.IntegrationEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AttemptService.Services;

public class AttemptDeadlineSubmissionService : BackgroundService
{
    private readonly IDbContextFactory<AttemptDbContext> _dbContextFactory;
    private readonly AttemptSubmissionOutboxWriter _outboxWriter;
    private readonly AttemptDeadlineSubmissionOptions _options;
    private readonly ILogger<AttemptDeadlineSubmissionService> _logger;

    public AttemptDeadlineSubmissionService(
        IDbContextFactory<AttemptDbContext> dbContextFactory,
        AttemptSubmissionOutboxWriter outboxWriter,
        IOptions<AttemptDeadlineSubmissionOptions> options,
        ILogger<AttemptDeadlineSubmissionService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _outboxWriter = outboxWriter;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Attempt deadline submission service is disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SubmitDueAttemptsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Could not submit due attempts.");
            }

            await Task.Delay(
                TimeSpan.FromSeconds(_options.PollIntervalSeconds),
                stoppingToken);
        }
    }

    public async Task<int> SubmitDueAttemptsAsync(CancellationToken cancellationToken)
    {
        var submittedCount = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var now = DateTimeOffset.UtcNow;
            var attempts = await dbContext.TestAttempts
                .Where(attempt => attempt.Status == TestAttemptStatus.InProgress
                    && attempt.EndsAt <= now)
                .OrderBy(attempt => attempt.EndsAt)
                .Take(_options.BatchSize)
                .ToListAsync(cancellationToken);
            if (attempts.Count == 0)
            {
                break;
            }

            var stagedAttemptCount = 0;
            foreach (var attempt in attempts)
            {
                attempt.Status = TestAttemptStatus.Submitted;
                attempt.SubmittedAt = attempt.EndsAt;
                attempt.SubmissionReason = AttemptSubmissionReason.Deadline;
                if (!await _outboxWriter.StageAsync(dbContext, attempt, cancellationToken))
                {
                    dbContext.Entry(attempt).State = EntityState.Unchanged;
                    continue;
                }

                attempt.StateRevision = checked(attempt.StateRevision + 1);
                stagedAttemptCount++;
            }

            if (stagedAttemptCount == 0)
            {
                break;
            }

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                submittedCount += stagedAttemptCount;
            }
            catch (DbUpdateConcurrencyException)
            {
                continue;
            }
            catch (DbUpdateException exception) when (IsDuplicateSubmissionEvent(exception))
            {
                continue;
            }

            if (attempts.Count < _options.BatchSize)
            {
                break;
            }
        }

        if (submittedCount > 0)
        {
            _logger.LogInformation(
                "Automatically submitted {AttemptCount} attempts at their deadline.",
                submittedCount);
        }

        return submittedCount;
    }

    private static bool IsDuplicateSubmissionEvent(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_AttemptSubmissionOutboxMessages_AttemptId"
        };
    }
}

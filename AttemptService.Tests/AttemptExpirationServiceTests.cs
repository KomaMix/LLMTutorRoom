using AttemptService.Contracts.Enums;
using AttemptService.Models;
using AttemptService.Options;
using AttemptService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AttemptService.Tests;

public sealed class AttemptExpirationServiceTests
{
    [Fact]
    public async Task ExpireDueAttempts_DrainsBatchesAndDoesNotCreateSubmissionEvents()
    {
        await using var factory = TestAttemptDbContextFactory.CreateInMemory();
        var now = DateTimeOffset.UtcNow;
        var dueAttempts = Enumerable.Range(1, 5)
            .Select(index => CreateAttempt(
                Guid.NewGuid(),
                TestAttemptStatus.InProgress,
                now.AddMinutes(-index),
                stateRevision: index))
            .ToList();
        var futureAttempt = CreateAttempt(
            Guid.NewGuid(),
            TestAttemptStatus.InProgress,
            now.AddMinutes(20),
            stateRevision: 3);
        var submittedAttempt = CreateAttempt(
            Guid.NewGuid(),
            TestAttemptStatus.Submitted,
            now.AddMinutes(-20),
            stateRevision: 4);

        await using (var dbContext = factory.CreateDbContext())
        {
            dbContext.TestAttempts.AddRange(dueAttempts);
            dbContext.TestAttempts.Add(futureAttempt);
            dbContext.TestAttempts.Add(submittedAttempt);
            await dbContext.SaveChangesAsync();
        }

        var service = new AttemptExpirationService(
            factory,
            Microsoft.Extensions.Options.Options.Create(
                new AttemptExpirationOptions { BatchSize = 2 }),
            NullLogger<AttemptExpirationService>.Instance);

        var expiredCount = await service.ExpireDueAttemptsAsync(CancellationToken.None);

        Assert.Equal(5, expiredCount);
        await using var verificationContext = factory.CreateDbContext();
        var dueAttemptIds = dueAttempts.Select(attempt => attempt.Id).ToList();
        var storedDueAttempts = await verificationContext.TestAttempts
            .Where(attempt => dueAttemptIds.Contains(attempt.Id))
            .OrderBy(attempt => attempt.Id)
            .ToListAsync();
        Assert.All(storedDueAttempts, attempt =>
        {
            Assert.Equal(TestAttemptStatus.Expired, attempt.Status);
            var original = dueAttempts.Single(item => item.Id == attempt.Id);
            Assert.Equal(original.StateRevision + 1, attempt.StateRevision);
        });

        var storedFutureAttempt = await verificationContext.TestAttempts
            .SingleAsync(attempt => attempt.Id == futureAttempt.Id);
        var storedSubmittedAttempt = await verificationContext.TestAttempts
            .SingleAsync(attempt => attempt.Id == submittedAttempt.Id);
        Assert.Equal(TestAttemptStatus.InProgress, storedFutureAttempt.Status);
        Assert.Equal(3, storedFutureAttempt.StateRevision);
        Assert.Equal(TestAttemptStatus.Submitted, storedSubmittedAttempt.Status);
        Assert.Equal(4, storedSubmittedAttempt.StateRevision);
        Assert.Empty(verificationContext.AttemptSubmissionOutboxMessages);
    }

    private static TestAttempt CreateAttempt(
        Guid testId,
        TestAttemptStatus status,
        DateTimeOffset endsAt,
        int stateRevision)
    {
        return new TestAttempt
        {
            Id = Guid.NewGuid(),
            TestId = testId.ToString("D"),
            TestRevision = 1,
            StudentUserId = "student-1",
            Status = status,
            StartedAt = DateTimeOffset.UtcNow.AddHours(-1),
            EndsAt = endsAt,
            SubmittedAt = status == TestAttemptStatus.Submitted
                ? DateTimeOffset.UtcNow.AddMinutes(-10)
                : null,
            AnswersJson = "{}",
            AllowedTaskIdsJson = "[]",
            StateRevision = stateRevision
        };
    }
}

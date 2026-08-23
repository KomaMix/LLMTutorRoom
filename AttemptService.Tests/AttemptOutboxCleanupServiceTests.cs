using AttemptService.Models;
using AttemptService.Options;
using AttemptService.Services.IntegrationEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AttemptService.Tests;

public sealed class AttemptOutboxCleanupServiceTests
{
    [Fact]
    public async Task DeleteExpiredMessages_DrainsBacklogAndPreservesPendingAndRecentMessages()
    {
        const int cleanupBatchSize = 3;
        const int expiredMessageCount = cleanupBatchSize * 2 + 1;

        await using var factory = await TestAttemptDbContextFactory.CreateSqliteAsync();
        var now = DateTimeOffset.UtcNow;
        var expiredMessages = Enumerable.Range(0, expiredMessageCount)
            .Select(index => CreateMessage(
                Guid.NewGuid(),
                now.AddDays(-40).AddMinutes(index),
                now.AddDays(-35).AddMinutes(index)))
            .ToList();
        var pendingMessage = CreateMessage(Guid.NewGuid(), now.AddDays(-40));
        var recentMessage = CreateMessage(Guid.NewGuid(), now.AddDays(-2), now.AddDays(-1));

        await using (var dbContext = factory.CreateDbContext())
        {
            dbContext.AttemptSubmissionOutboxMessages.AddRange(expiredMessages);
            dbContext.AttemptSubmissionOutboxMessages.Add(pendingMessage);
            dbContext.AttemptSubmissionOutboxMessages.Add(recentMessage);
            await dbContext.SaveChangesAsync();
        }

        var service = new AttemptOutboxCleanupService(
            factory,
            Microsoft.Extensions.Options.Options.Create(
                new AttemptOutboxOptions
                {
                    PublishedMessageRetentionDays = 30,
                    CleanupBatchSize = cleanupBatchSize
                }),
            NullLogger<AttemptOutboxCleanupService>.Instance);

        var deleted = await service.DeleteExpiredMessagesAsync(CancellationToken.None);

        Assert.Equal(expiredMessageCount, deleted);
        await using var verificationContext = factory.CreateDbContext();
        var remainingMessages = await verificationContext.AttemptSubmissionOutboxMessages
            .AsNoTracking()
            .ToListAsync();
        Assert.Equal(2, remainingMessages.Count);
        Assert.Contains(remainingMessages, message => message.Id == pendingMessage.Id);
        Assert.Contains(remainingMessages, message => message.Id == recentMessage.Id);
        Assert.DoesNotContain(
            remainingMessages,
            message => expiredMessages.Any(expired => expired.Id == message.Id));
    }

    private static AttemptSubmissionOutboxMessage CreateMessage(
        Guid attemptId,
        DateTimeOffset occurredAt,
        DateTimeOffset? publishedAt = null)
    {
        return new AttemptSubmissionOutboxMessage
        {
            Id = Guid.NewGuid(),
            AttemptId = attemptId,
            OccurredAt = occurredAt,
            PayloadJson = "{}",
            PublishedAt = publishedAt
        };
    }
}

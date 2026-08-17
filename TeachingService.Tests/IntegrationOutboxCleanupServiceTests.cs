using TeachingService.Services.IntegrationEvents;
using TeachingService.Models;

namespace TeachingService.Tests;

public sealed class IntegrationOutboxCleanupServiceTests
{
    [Fact]
    public async Task DrainBatches_DeletesEntireExpiredBacklogAndKeepsPendingAndRecentMessages()
    {
        const int cleanupBatchSize = 3;
        const int expiredMessageCount = cleanupBatchSize * 2 + 1;

        var now = DateTimeOffset.UtcNow;
        var cutoff = now.AddDays(-30);
        var messages = Enumerable.Range(0, expiredMessageCount)
            .Select(index => CreateMessage(
                now.AddDays(-40).AddMinutes(index),
                now.AddDays(-31).AddMinutes(index)))
            .ToList();
        var pendingMessage = CreateMessage(now.AddDays(-40));
        var recentMessage = CreateMessage(now.AddDays(-2), now.AddDays(-1));
        messages.Add(pendingMessage);
        messages.Add(recentMessage);
        var batchCalls = 0;

        Task<(int Selected, int Deleted)> DeleteBatchAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            batchCalls++;
            var selected = messages
                .Where(message => message.PublishedAt != null
                    && message.PublishedAt < cutoff)
                .OrderBy(message => message.PublishedAt)
                .Take(cleanupBatchSize)
                .ToArray();

            foreach (var message in selected)
                messages.Remove(message);

            return Task.FromResult((selected.Length, selected.Length));
        }

        var deleted = await IntegrationOutboxCleanupService.DrainBatchesAsync(
            DeleteBatchAsync,
            CancellationToken.None);

        Assert.Equal(expiredMessageCount, deleted);
        Assert.Equal(4, batchCalls);
        Assert.Equal(2, messages.Count);
        Assert.Contains(messages, message => message.Id == pendingMessage.Id);
        Assert.Contains(messages, message => message.Id == recentMessage.Id);
    }

    [Fact]
    public async Task DrainBatches_DoesNotStartAnotherBatchAfterCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var batchWasCalled = false;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            IntegrationOutboxCleanupService.DrainBatchesAsync(
                _ =>
                {
                    batchWasCalled = true;
                    return Task.FromResult((Selected: 0, Deleted: 0));
                },
                cancellation.Token));

        Assert.False(batchWasCalled);
    }

    [Fact]
    public async Task DrainBatches_StopsWhenAnotherCleanerDeletedTheSelectedBatch()
    {
        var batchCalls = 0;

        var deleted = await IntegrationOutboxCleanupService.DrainBatchesAsync(
            _ =>
            {
                batchCalls++;
                return Task.FromResult((Selected: 3, Deleted: 0));
            },
            CancellationToken.None);

        Assert.Equal(0, deleted);
        Assert.Equal(1, batchCalls);
    }

    private static IntegrationOutboxMessage CreateMessage(
        DateTimeOffset occurredAt,
        DateTimeOffset? publishedAt = null)
    {
        var message = new IntegrationOutboxMessage(
            Guid.NewGuid(),
            "TestEvent",
            "test.event",
            "{}",
            occurredAt);

        if (publishedAt.HasValue)
            message.MarkPublished(publishedAt.Value);

        return message;
    }
}

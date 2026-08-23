using System.Text.Json;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ReviewService.Contracts.Enums;
using ReviewService.Contracts.Events;
using ReviewService.Data;
using ReviewService.Helpers;
using ReviewService.Interfaces;
using ReviewService.Models.Reviews;

namespace ReviewService.Services;

public sealed class ReviewIntegrationEventHandler(
    ReviewDbContext dbContext,
    IReviewCreationService reviewCreationService,
    IReviewQueuePublisher queuePublisher,
    ILogger<ReviewIntegrationEventHandler> logger) : IReviewIntegrationEventHandler
{
    public async Task HandleAsync(
        AttemptSubmittedV1 message,
        CancellationToken cancellationToken)
    {
        Validate(message);
        if (await IsProcessedAsync(message.EventId, cancellationToken))
            return;

        var reviewsToPublish = new List<Review>();
        await using (var transaction = await BeginTransactionAsync(cancellationToken))
        {
            dbContext.InboxMessages.Add(CreateInbox(message.EventId, nameof(AttemptSubmittedV1)));

            var attemptAlreadyKnown = await dbContext.Reviews.AnyAsync(
                    review => review.AttemptId == message.AttemptId,
                    cancellationToken)
                || await dbContext.PendingSubmissions.AnyAsync(
                    submission => submission.AttemptId == message.AttemptId,
                    cancellationToken);
            if (!attemptAlreadyKnown)
            {
                var policy = await dbContext.TestReviewPolicies.SingleOrDefaultAsync(
                    item => item.TestId == message.TestId && item.Revision == message.TestRevision,
                    cancellationToken);
                if (policy is null)
                {
                    dbContext.PendingSubmissions.Add(ToPendingSubmission(message));
                }
                else
                {
                    reviewsToPublish.Add(await reviewCreationService.CreateAsync(
                        policy,
                        message.AttemptId,
                        message.StudentUserId,
                        message.StudentName,
                        message.Answers,
                        message.SubmittedAt,
                        cancellationToken));
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        }

        await TryPublishAsync(reviewsToPublish, cancellationToken);
    }

    public async Task HandleAsync(
        TestReviewPolicyPublishedV1 message,
        CancellationToken cancellationToken)
    {
        Validate(message);
        if (await IsProcessedAsync(message.EventId, cancellationToken))
            return;

        var reviewsToPublish = new List<Review>();
        await using (var transaction = await BeginTransactionAsync(cancellationToken))
        {
            dbContext.InboxMessages.Add(CreateInbox(message.EventId, nameof(TestReviewPolicyPublishedV1)));

            var policy = await dbContext.TestReviewPolicies.SingleOrDefaultAsync(
                item => item.TestId == message.TestId && item.Revision == message.Revision,
                cancellationToken);
            if (policy is null)
            {
                policy = new TestReviewPolicy
                {
                    TestId = message.TestId,
                    Revision = message.Revision,
                    TeacherUserId = message.TeacherUserId,
                    TestTitle = message.TestTitle,
                    ModelKeySnapshot = message.ModelKey?.Trim() ?? string.Empty,
                    TasksJson = JsonSerializer.Serialize(message.Tasks, JsonHelper.Options),
                    PublishedAt = message.PublishedAt,
                    ReceivedAt = DateTimeOffset.UtcNow
                };
                dbContext.TestReviewPolicies.Add(policy);
            }

            var pendingSubmissions = await dbContext.PendingSubmissions
                .Where(item => item.TestId == message.TestId && item.TestRevision == message.Revision)
                .OrderBy(item => item.SubmittedAt)
                .ToListAsync(cancellationToken);
            foreach (var pending in pendingSubmissions)
            {
                if (await dbContext.Reviews.AnyAsync(
                        review => review.AttemptId == pending.AttemptId,
                        cancellationToken))
                {
                    dbContext.PendingSubmissions.Remove(pending);
                    continue;
                }

                var answers = JsonSerializer.Deserialize<Dictionary<string, string>>(
                        pending.AnswersJson,
                        JsonHelper.Options)
                    ?? [];
                reviewsToPublish.Add(await reviewCreationService.CreateAsync(
                    policy,
                    pending.AttemptId,
                    pending.StudentUserId,
                    pending.StudentName,
                    answers,
                    pending.SubmittedAt,
                    cancellationToken));
                dbContext.PendingSubmissions.Remove(pending);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        }

        await TryPublishAsync(reviewsToPublish, cancellationToken);
    }

    private async Task TryPublishAsync(
        IEnumerable<Review> reviews,
        CancellationToken cancellationToken)
    {
        foreach (var review in reviews.Where(review => review.Status == ReviewStatus.Queued))
        {
            try
            {
                await queuePublisher.PublishAsync(
                    new ReviewQueueMessage
                    {
                        ReviewId = review.Id,
                        AttemptId = review.AttemptId,
                        ModelKey = review.ModelKeySnapshot,
                        RequestedAt = DateTimeOffset.UtcNow
                    },
                    retryDelaySeconds: null,
                    cancellationToken);
                var enqueuedAt = DateTimeOffset.UtcNow;
                if (dbContext.Database.IsRelational())
                {
                    await dbContext.Reviews
                        .Where(item => item.Id == review.Id)
                        .ExecuteUpdateAsync(
                            setters => setters.SetProperty(
                                item => item.LastEnqueuedAt,
                                enqueuedAt),
                            cancellationToken);
                }
                else
                {
                    review.LastEnqueuedAt = enqueuedAt;
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(
                    exception,
                    "Could not enqueue review {ReviewId}; queue maintenance will retry.",
                    review.Id);
            }
        }
    }

    private Task<bool> IsProcessedAsync(Guid eventId, CancellationToken cancellationToken)
    {
        return dbContext.InboxMessages.AnyAsync(item => item.EventId == eventId, cancellationToken);
    }

    private InboxMessage CreateInbox(Guid eventId, string eventType)
    {
        return new InboxMessage
        {
            EventId = eventId,
            EventType = eventType,
            ProcessedAt = DateTimeOffset.UtcNow
        };
    }

    private PendingSubmission ToPendingSubmission(AttemptSubmittedV1 message)
    {
        return new PendingSubmission
        {
            AttemptId = message.AttemptId,
            TestId = message.TestId,
            TestRevision = message.TestRevision,
            StudentUserId = message.StudentUserId,
            StudentName = string.IsNullOrWhiteSpace(message.StudentName)
                ? "Студент"
                : message.StudentName.Trim(),
            AnswersJson = JsonSerializer.Serialize(message.Answers, JsonHelper.Options),
            SubmittedAt = message.SubmittedAt,
            ReceivedAt = DateTimeOffset.UtcNow
        };
    }

    private async Task<IDbContextTransaction?> BeginTransactionAsync(
        CancellationToken cancellationToken)
    {
        return dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken)
            : null;
    }

    private static void Validate(AttemptSubmittedV1 message)
    {
        if (message.EventId == Guid.Empty
            || message.AttemptId == Guid.Empty
            || string.IsNullOrWhiteSpace(message.TestId)
            || message.TestRevision <= 0
            || string.IsNullOrWhiteSpace(message.StudentUserId)
            || message.Answers is null
            || message.Answers.Keys.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidDataException("AttemptSubmittedV1 contains invalid required values.");
        }
    }

    private static void Validate(TestReviewPolicyPublishedV1 message)
    {
        if (message.EventId == Guid.Empty
            || string.IsNullOrWhiteSpace(message.TestId)
            || message.Revision <= 0
            || string.IsNullOrWhiteSpace(message.TeacherUserId)
            || string.IsNullOrWhiteSpace(message.TestTitle)
            || message.Tasks is null)
        {
            throw new InvalidDataException("TestReviewPolicyPublishedV1 contains invalid required values.");
        }

        var taskIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var task in message.Tasks)
        {
            if (string.IsNullOrWhiteSpace(task.Id)
                || !taskIds.Add(task.Id)
                || string.IsNullOrWhiteSpace(task.Title)
                || string.IsNullOrWhiteSpace(task.Prompt)
                || task.MaxPoints < 0
                || task.WrongAnswerPenalty < 0
                || !Enum.IsDefined(task.Type)
                || !Enum.IsDefined(task.CheckMode)
                || task.Options is null)
            {
                throw new InvalidDataException(
                    "TestReviewPolicyPublishedV1 contains an invalid task snapshot.");
            }

            var optionIds = new HashSet<string>(StringComparer.Ordinal);
            if (task.Options.Any(option => string.IsNullOrWhiteSpace(option.Id)
                    || string.IsNullOrWhiteSpace(option.Text)
                    || !optionIds.Add(option.Id)))
            {
                throw new InvalidDataException(
                    $"Review task '{task.Id}' contains invalid or duplicate answer options.");
            }

            var correctOptionCount = task.Options.Count(option => option.IsCorrect);
            var validShape = task.Type switch
            {
                ReviewTaskType.SingleChoice => task.CheckMode == ReviewCheckMode.Auto
                    && task.Options.Count >= 2
                    && correctOptionCount == 1,
                ReviewTaskType.MultipleChoice => task.CheckMode == ReviewCheckMode.Auto
                    && task.Options.Count >= 2
                    && correctOptionCount >= 1,
                ReviewTaskType.FreeText => task.Options.Count == 0,
                _ => false
            };
            if (!validShape)
            {
                throw new InvalidDataException(
                    $"Review task '{task.Id}' has an invalid type, check mode, or answer-option shape.");
            }
        }
    }
}

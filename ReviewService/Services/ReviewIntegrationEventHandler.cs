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

public class ReviewIntegrationEventHandler : IReviewIntegrationEventHandler
{
    private readonly ReviewDbContext _dbContext;
    private readonly IReviewCreationService _reviewCreationService;
    private readonly IReviewQueuePublisher _queuePublisher;
    private readonly ILogger<ReviewIntegrationEventHandler> _logger;

    public ReviewIntegrationEventHandler(
        ReviewDbContext dbContext,
        IReviewCreationService reviewCreationService,
        IReviewQueuePublisher queuePublisher,
        ILogger<ReviewIntegrationEventHandler> logger)
    {
        _dbContext = dbContext;
        _reviewCreationService = reviewCreationService;
        _queuePublisher = queuePublisher;
        _logger = logger;
    }

    public async Task HandleAsync(
        AttemptSubmittedV1 message,
        CancellationToken cancellationToken)
    {
        Validate(message);
        if (await IsProcessedAsync(message.EventId, cancellationToken))
        {
            return;
        }

        var reviewsToPublish = new List<Review>();
        await using (var transaction = await BeginTransactionAsync(cancellationToken))
        {
            _dbContext.InboxMessages.Add(CreateInbox(message.EventId, nameof(AttemptSubmittedV1)));

            var attemptAlreadyKnown = await _dbContext.Reviews.AnyAsync(
                    review => review.AttemptId == message.AttemptId,
                    cancellationToken)
                || await _dbContext.PendingSubmissions.AnyAsync(
                    submission => submission.AttemptId == message.AttemptId,
                    cancellationToken);
            if (!attemptAlreadyKnown)
            {
                var policy = await _dbContext.TestReviewPolicies.SingleOrDefaultAsync(
                    item => item.TestId == message.TestId && item.Revision == message.TestRevision,
                    cancellationToken);
                if (policy is null)
                {
                    _dbContext.PendingSubmissions.Add(ToPendingSubmission(message));
                }
                else
                {
                    reviewsToPublish.Add(await _reviewCreationService.CreateAsync(
                        policy,
                        message.AttemptId,
                        message.AssignmentId,
                        message.GroupId,
                        message.StudentUserId,
                        message.StudentName,
                        message.Answers,
                        message.SubmittedAt,
                        cancellationToken));
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }

        await TryPublishAsync(reviewsToPublish, cancellationToken);
    }

    public async Task HandleAsync(
        TestReviewPolicyPublishedV1 message,
        CancellationToken cancellationToken)
    {
        Validate(message);
        if (await IsProcessedAsync(message.EventId, cancellationToken))
        {
            return;
        }

        var reviewsToPublish = new List<Review>();
        await using (var transaction = await BeginTransactionAsync(cancellationToken))
        {
            _dbContext.InboxMessages.Add(CreateInbox(message.EventId, nameof(TestReviewPolicyPublishedV1)));

            var policy = await _dbContext.TestReviewPolicies.SingleOrDefaultAsync(
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
                    ModelId = message.ModelId,
                    FeedbackTemplateJson = JsonSerializer.Serialize(
                        message.FeedbackTemplate,
                        JsonHelper.Options),
                    TasksJson = JsonSerializer.Serialize(message.Tasks, JsonHelper.Options),
                    GradingExamplesJson = JsonSerializer.Serialize(message.GradingExamples, JsonHelper.Options),
                    PublishedAt = message.PublishedAt,
                    ReceivedAt = DateTimeOffset.UtcNow
                };
                _dbContext.TestReviewPolicies.Add(policy);
            }

            var pendingSubmissions = await _dbContext.PendingSubmissions
                .Where(item => item.TestId == message.TestId && item.TestRevision == message.Revision)
                .OrderBy(item => item.SubmittedAt)
                .ToListAsync(cancellationToken);
            foreach (var pending in pendingSubmissions)
            {
                if (await _dbContext.Reviews.AnyAsync(
                        review => review.AttemptId == pending.AttemptId,
                        cancellationToken))
                {
                    _dbContext.PendingSubmissions.Remove(pending);
                    continue;
                }

                var answers = JsonSerializer.Deserialize<Dictionary<string, string>>(
                        pending.AnswersJson,
                        JsonHelper.Options)
                    ?? [];
                reviewsToPublish.Add(await _reviewCreationService.CreateAsync(
                    policy,
                    pending.AttemptId,
                    pending.AssignmentId,
                    pending.GroupId,
                    pending.StudentUserId,
                    pending.StudentName,
                    answers,
                    pending.SubmittedAt,
                    cancellationToken));
                _dbContext.PendingSubmissions.Remove(pending);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
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
                await _queuePublisher.PublishAsync(
                    new ReviewQueueMessage
                    {
                        ReviewId = review.Id,
                        AttemptId = review.AttemptId,
                        RequestedAt = DateTimeOffset.UtcNow
                    },
                    retryDelaySeconds: null,
                    cancellationToken);
                var enqueuedAt = DateTimeOffset.UtcNow;
                if (_dbContext.Database.IsRelational())
                {
                    await _dbContext.Reviews
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
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(
                    exception,
                    "Could not enqueue review {ReviewId}; queue maintenance will retry.",
                    review.Id);
            }
        }
    }

    private Task<bool> IsProcessedAsync(Guid eventId, CancellationToken cancellationToken)
    {
        return _dbContext.InboxMessages.AnyAsync(item => item.EventId == eventId, cancellationToken);
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
            AssignmentId = message.AssignmentId,
            GroupId = message.GroupId,
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
        return _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken)
            : null;
    }

    private static void Validate(AttemptSubmittedV1 message)
    {
        if (message.EventId == Guid.Empty
            || message.AttemptId == Guid.Empty
            || message.AssignmentId == Guid.Empty
            || message.GroupId == Guid.Empty
            || string.IsNullOrWhiteSpace(message.TestId)
            || message.TestRevision <= 0
            || string.IsNullOrWhiteSpace(message.StudentUserId)
            || message.Answers is null
            || message.Answers.Keys.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidDataException("AttemptSubmittedV1 contains invalid required values.");
        }
    }

    private static void ValidateGradingExamples(
        IList<GradingExampleSnapshot>? examples,
        bool requireTaskPrompt)
    {
        if (examples is null || examples.Count > 5)
        {
            throw new InvalidDataException("The policy must contain at most five grading examples at each level.");
        }

        foreach (var example in examples)
        {
            if (example is null
                || (requireTaskPrompt && string.IsNullOrWhiteSpace(example.TaskPrompt))
                || string.IsNullOrWhiteSpace(example.StudentAnswer)
                || string.IsNullOrWhiteSpace(example.Feedback)
                || example.MaxScore <= 0
                || example.Score < 0
                || example.Score > example.MaxScore
                || (example.TaskPrompt?.Length ?? 0) > 4000
                || example.StudentAnswer.Length > 8000
                || example.Feedback.Length > 4000)
            {
                throw new InvalidDataException("The policy contains an invalid grading example.");
            }
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

        if (message.ModelId is <= 0
            || (message.Tasks.Any(task => task.CheckMode == ReviewCheckMode.Llm) && message.ModelId is null))
        {
            throw new InvalidDataException("An LLM review policy must reference a positive model ID.");
        }

        ValidateFeedbackTemplate(message.FeedbackTemplate);

        ValidateGradingExamples(message.GradingExamples, requireTaskPrompt: true);

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

            ValidateGradingExamples(task.GradingExamples, requireTaskPrompt: false);
            if (task.Type != ReviewTaskType.FreeText && task.GradingExamples.Count > 0)
            {
                throw new InvalidDataException("Grading examples are supported only for free-text tasks.");
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
                ReviewTaskType.FreeText => (task.CheckMode is ReviewCheckMode.Llm
                        or ReviewCheckMode.Manual)
                    && task.Options.Count == 0,
                _ => false
            };
            if (!validShape)
            {
                throw new InvalidDataException(
                    $"Review task '{task.Id}' has an invalid type, check mode, or answer-option shape.");
            }
        }
    }

    private static void ValidateFeedbackTemplate(FeedbackTemplateSnapshot? template)
    {
        if (template is null
            || template.Id == Guid.Empty
            || string.IsNullOrWhiteSpace(template.Name)
            || template.Name.Length > 120
            || (template.CustomInstruction?.Length ?? 0) > 2000
            || !Enum.IsDefined(template.Tone)
            || !Enum.IsDefined(template.DetailLevel)
            || !Enum.IsDefined(template.AnswerDisclosure)
            || template.Sections is null
            || template.Sections.Count is < 1 or > 6)
        {
            throw new InvalidDataException("The policy contains an invalid feedback template.");
        }

        var sectionIds = new HashSet<Guid>();
        foreach (var section in template.Sections)
        {
            if (section is null
                || section.Id == Guid.Empty
                || !sectionIds.Add(section.Id)
                || string.IsNullOrWhiteSpace(section.Title)
                || section.Title.Length > 120
                || string.IsNullOrWhiteSpace(section.Instruction)
                || section.Instruction.Length > 1000
                || !Enum.IsDefined(section.Format)
                || section.MaxItems is < 1 or > 5
                || section.Format == FeedbackSectionFormat.Text && section.MaxItems != 1)
            {
                throw new InvalidDataException("The policy contains an invalid feedback template section.");
            }
        }
    }
}

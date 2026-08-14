using System.Text.Json;
using LLMTutorRoom.Data;
using LLMTutorRoom.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TeachingService.Contracts.Enums;

namespace LLMTutorRoom.Services.ReviewProcessing
{
    public sealed class ReviewJobProcessor
    {
        private readonly TutorRoomDbContext _dbContext;
        private readonly ReviewScoringService _scoringService;
        private readonly LlmGatewayReviewClient _llmGatewayReviewClient;
        private readonly RabbitMqReviewTopology _topology;
        private readonly RabbitMqOptions _rabbitMqOptions;
        private readonly ReviewProcessingOptions _processingOptions;
        private readonly ILogger<ReviewJobProcessor> _logger;

        public ReviewJobProcessor(
            TutorRoomDbContext dbContext,
            ReviewScoringService scoringService,
            LlmGatewayReviewClient llmGatewayReviewClient,
            RabbitMqReviewTopology topology,
            IOptions<RabbitMqOptions> rabbitMqOptions,
            IOptions<ReviewProcessingOptions> processingOptions,
            ILogger<ReviewJobProcessor> logger)
        {
            _dbContext = dbContext;
            _scoringService = scoringService;
            _llmGatewayReviewClient = llmGatewayReviewClient;
            _topology = topology;
            _rabbitMqOptions = rabbitMqOptions.Value;
            _processingOptions = processingOptions.Value;
            _logger = logger;
        }

        public async Task<ReviewProcessingOutcome> ProcessAsync(
            ReviewQueueMessage message,
            CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            var review = await _dbContext.SubmissionReviews
                .Include(item => item.TaskResults)
                .SingleOrDefaultAsync(item => item.Id == message.ReviewId, cancellationToken);

            if (review is null)
            {
                _logger.LogWarning(
                    "Review {ReviewId} was not found while processing queue message.",
                    message.ReviewId);
                return ReviewProcessingOutcome.Poison();
            }

            if (review.AttemptId != message.AttemptId)
            {
                _logger.LogWarning(
                    "Review {ReviewId} belongs to attempt {ActualAttemptId}, but message requested attempt {MessageAttemptId}.",
                    review.Id,
                    review.AttemptId,
                    message.AttemptId);
                return ReviewProcessingOutcome.Poison();
            }

            if (!ShouldProcess(review, now))
                return ReviewProcessingOutcome.Ignored();

            var activePause = await GetActiveTestLlmPauseAsync(
                review.TestId,
                review.ModelKey,
                now,
                cancellationToken);
            if (activePause is not null)
            {
                MarkReviewPaused(
                    review,
                    activePause.LastError,
                    activePause.PausedUntil);
                await _dbContext.SaveChangesAsync(cancellationToken);
                return ReviewProcessingOutcome.Paused();
            }

            review.Status = SubmissionReviewStatus.Processing;
            review.StartedAt ??= now;
            review.ProcessingAttempts++;
            review.ProcessingLeaseExpiresAt = now.AddSeconds(_processingOptions.ProcessingLeaseSeconds);
            review.NextRetryAt = null;
            await _dbContext.SaveChangesAsync(cancellationToken);

            var attempt = await _dbContext.TestAttempts
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == message.AttemptId, cancellationToken);

            if (attempt is null)
            {
                MarkReviewFailed(review, "Attempt was not found.");
                await _dbContext.SaveChangesAsync(cancellationToken);
                return ReviewProcessingOutcome.Poison();
            }

            var answers = DeserializeAnswers(attempt.AnswersJson);
            var retryDelaySeconds = 0;
            DateTimeOffset? pauseUntil = null;

            foreach (var result in review.TaskResults.Where(result => result.CheckMode == TestTaskCheckMode.Llm))
            {
                if (result.Status == TaskReviewResultStatus.Succeeded
                    || result.Status == TaskReviewResultStatus.ManualReview)
                {
                    continue;
                }

                answers.TryGetValue(result.TaskId, out var answer);
                result.Status = TaskReviewResultStatus.Processing;
                await _dbContext.SaveChangesAsync(cancellationToken);

                try
                {
                    var llmResult = await _llmGatewayReviewClient.ReviewFreeTextAnswerAsync(
                        review.ModelKey,
                        result,
                        answer ?? string.Empty,
                        cancellationToken);

                    _scoringService.CreateLlmResult(result, llmResult);
                }
                catch (LlmGatewayReviewDisabledException ex)
                {
                    pauseUntil = await PauseTestLlmProcessingAsync(
                        review,
                        result,
                        ex.Message,
                        now,
                        cancellationToken);
                    break;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(
                        ex,
                        "LLM review for review {ReviewId}, attempt {AttemptId}, task {TaskId} failed.",
                        review.Id,
                        attempt.Id,
                        result.TaskId);

                    var delaySeconds = MarkTaskForRetryOrPause(result, ex);
                    if (delaySeconds > 0)
                    {
                        retryDelaySeconds = Math.Max(retryDelaySeconds, delaySeconds);
                        continue;
                    }

                    pauseUntil = await PauseTestLlmProcessingAsync(
                        review,
                        result,
                        ex.Message,
                        now,
                        cancellationToken);
                    break;
                }
            }

            _scoringService.RecalculateReview(review);

            if (pauseUntil.HasValue)
            {
                MarkReviewPaused(
                    review,
                    CreateReviewErrorSummary(review),
                    pauseUntil.Value);
                await _dbContext.SaveChangesAsync(cancellationToken);
                return ReviewProcessingOutcome.Paused();
            }

            if (retryDelaySeconds > 0)
            {
                review.Status = SubmissionReviewStatus.RetryScheduled;
                review.LastError = CreateReviewErrorSummary(review);
                review.NextRetryAt = now.AddSeconds(retryDelaySeconds);
                review.ProcessingLeaseExpiresAt = null;
                await _dbContext.SaveChangesAsync(cancellationToken);
                return ReviewProcessingOutcome.RetryScheduled(retryDelaySeconds);
            }

            review.Status = _scoringService.GetReviewStatusAfterTaskProcessing(review);
            review.CompletedAt = review.Status == SubmissionReviewStatus.Checked
                ? DateTimeOffset.UtcNow
                : null;
            review.ProcessingLeaseExpiresAt = null;
            review.NextRetryAt = null;
            review.LastError = string.Empty;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return ReviewProcessingOutcome.Completed();
        }

        private bool ShouldProcess(SubmissionReview review, DateTimeOffset now)
        {
            if (review.Status is SubmissionReviewStatus.Checked
                or SubmissionReviewStatus.ManualReview
                or SubmissionReviewStatus.Failed)
            {
                return false;
            }

            if (review.Status == SubmissionReviewStatus.Paused
                && review.NextRetryAt.HasValue
                && review.NextRetryAt.Value > now)
            {
                return false;
            }

            if (review.Status == SubmissionReviewStatus.Processing
                && review.ProcessingLeaseExpiresAt.HasValue
                && review.ProcessingLeaseExpiresAt.Value > now)
            {
                return false;
            }

            if (review.Status == SubmissionReviewStatus.RetryScheduled
                && review.NextRetryAt.HasValue
                && review.NextRetryAt.Value > now)
            {
                return false;
            }

            return true;
        }

        private int MarkTaskForRetryOrPause(
            TaskReviewResult result,
            Exception exception)
        {
            result.Attempts++;
            result.LastError = exception.Message;

            if (result.Attempts > _rabbitMqOptions.RetryDelaysSeconds.Length)
                return 0;

            var delaySeconds = _topology.GetRetryDelaySeconds(result.Attempts);
            result.Status = TaskReviewResultStatus.RetryScheduled;
            result.NextRetryAt = DateTimeOffset.UtcNow.AddSeconds(delaySeconds);
            return delaySeconds;
        }

        private async Task<TestLlmPause?> GetActiveTestLlmPauseAsync(
            string testId,
            string modelKey,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(testId) || string.IsNullOrWhiteSpace(modelKey))
                return null;

            return await _dbContext.TestLlmPauses
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    pause => pause.TestId == testId
                        && pause.ModelKey == modelKey
                        && pause.PausedUntil > now,
                    cancellationToken);
        }

        private async Task<DateTimeOffset> PauseTestLlmProcessingAsync(
            SubmissionReview review,
            TaskReviewResult result,
            string error,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            var pauseUntil = now.AddSeconds(Math.Max(1, _processingOptions.TestLlmFailurePauseSeconds));
            MarkTaskPaused(result, error, pauseUntil);

            var pause = await _dbContext.TestLlmPauses
                .SingleOrDefaultAsync(
                    item => item.TestId == review.TestId && item.ModelKey == review.ModelKey,
                    cancellationToken);

            if (pause is null)
            {
                pause = new TestLlmPause
                {
                    TestId = review.TestId,
                    ModelKey = review.ModelKey,
                    CreatedAt = DateTime.UtcNow
                };
                _dbContext.TestLlmPauses.Add(pause);
            }

            pause.PausedUntil = pauseUntil;
            pause.LastError = error;
            pause.FailureCount++;
            pause.UpdatedAt = DateTime.UtcNow;

            return pauseUntil;
        }

        private static void MarkTaskPaused(
            TaskReviewResult result,
            string error,
            DateTimeOffset pauseUntil)
        {
            result.Status = TaskReviewResultStatus.Paused;
            result.NextRetryAt = pauseUntil;
            result.LastError = error;
        }

        private static void MarkReviewPaused(
            SubmissionReview review,
            string error,
            DateTimeOffset pauseUntil)
        {
            foreach (var result in review.TaskResults
                         .Where(result => result.CheckMode == TestTaskCheckMode.Llm
                             && result.Status is not TaskReviewResultStatus.Succeeded
                             && result.Status is not TaskReviewResultStatus.ManualReview))
            {
                MarkTaskPaused(result, error, pauseUntil);
            }

            review.Status = SubmissionReviewStatus.Paused;
            review.LastError = error;
            review.NextRetryAt = pauseUntil;
            review.ProcessingLeaseExpiresAt = null;
        }

        private static void MarkReviewFailed(SubmissionReview review, string error)
        {
            review.Status = SubmissionReviewStatus.Failed;
            review.LastError = error;
            review.ProcessingLeaseExpiresAt = null;
            review.NextRetryAt = null;
            review.CompletedAt = DateTimeOffset.UtcNow;
        }

        private static string CreateReviewErrorSummary(SubmissionReview review)
        {
            var failedTask = review.TaskResults
                .Where(result => result.Status == TaskReviewResultStatus.RetryScheduled)
                .Concat(review.TaskResults.Where(result => result.Status == TaskReviewResultStatus.Paused))
                .OrderByDescending(result => result.NextRetryAt)
                .FirstOrDefault();

            return failedTask is null
                ? string.Empty
                : $"Task {failedTask.TaskId}: {failedTask.LastError}";
        }

        private static Dictionary<string, string> DeserializeAnswers(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new Dictionary<string, string>();

            return JsonSerializer.Deserialize<Dictionary<string, string>>(
                    json,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?? new Dictionary<string, string>();
        }
    }
}

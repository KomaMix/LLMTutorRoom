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
                        result,
                        answer ?? string.Empty,
                        cancellationToken);

                    _scoringService.CreateLlmResult(result, llmResult);
                }
                catch (LlmGatewayReviewDisabledException ex)
                {
                    MarkTaskForManualReview(result, ex.Message);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    var delaySeconds = MarkTaskForRetryOrManualReview(result, ex);
                    retryDelaySeconds = Math.Max(retryDelaySeconds, delaySeconds);

                    _logger.LogWarning(
                        ex,
                        "LLM review for review {ReviewId}, attempt {AttemptId}, task {TaskId} failed.",
                        review.Id,
                        attempt.Id,
                        result.TaskId);
                }
            }

            _scoringService.RecalculateReview(review);

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

        private int MarkTaskForRetryOrManualReview(
            TaskReviewResult result,
            Exception exception)
        {
            result.Attempts++;
            result.LastError = exception.Message;

            if (result.Attempts > _rabbitMqOptions.RetryDelaysSeconds.Length)
            {
                MarkTaskForManualReview(result, exception.Message);
                return 0;
            }

            var delaySeconds = _topology.GetRetryDelaySeconds(result.Attempts);
            result.Status = TaskReviewResultStatus.RetryScheduled;
            result.NextRetryAt = DateTimeOffset.UtcNow.AddSeconds(delaySeconds);
            return delaySeconds;
        }

        private static void MarkTaskForManualReview(
            TaskReviewResult result,
            string error)
        {
            result.Status = TaskReviewResultStatus.ManualReview;
            result.NextRetryAt = null;
            result.LastError = error;
            result.Feedback = "Автоматическая проверка не завершилась. Требуется ручная проверка.";
            result.Findings = new List<string>
            {
                "Задание ожидает ручной проверки преподавателем."
            };
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

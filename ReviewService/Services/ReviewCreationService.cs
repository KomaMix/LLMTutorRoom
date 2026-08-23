using System.Text.Json;
using Microsoft.Extensions.Options;
using ReviewService.Options;
using ReviewService.Contracts.Enums;
using ReviewService.Contracts.Events;
using ReviewService.Data;
using ReviewService.Enums;
using ReviewService.Helpers;
using ReviewService.Interfaces;
using ReviewService.Models.ModelAccess;
using ReviewService.Models.Reviews;

namespace ReviewService.Services;

public sealed class ReviewCreationService(
    ReviewDbContext dbContext,
    IReviewScoringService scoringService,
    ITeacherModelAccessService modelAccessService,
    IOptions<ReviewProcessingOptions> processingOptions) : IReviewCreationService
{
    private readonly ReviewProcessingOptions _processingOptions = processingOptions.Value;

    public async Task<Review> CreateAsync(
        TestReviewPolicy policy,
        Guid attemptId,
        string studentUserId,
        string studentName,
        Dictionary<string, string> answers,
        DateTimeOffset submittedAt,
        CancellationToken cancellationToken)
    {
        var policyTasks = JsonSerializer.Deserialize<List<ReviewTaskPolicySnapshot>>(
                policy.TasksJson,
                JsonHelper.Options)
            ?? [];
        var duplicateTaskId = policyTasks
            .GroupBy(task => task.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateTaskId is not null)
            throw new InvalidDataException($"Review policy contains duplicate task '{duplicateTaskId}'.");

        var now = DateTimeOffset.UtcNow;
        var review = new Review
        {
            AttemptId = attemptId,
            TestId = policy.TestId,
            TestRevision = policy.Revision,
            TestTitle = policy.TestTitle,
            TeacherUserId = policy.TeacherUserId,
            StudentUserId = studentUserId,
            StudentName = string.IsNullOrWhiteSpace(studentName) ? null : studentName.Trim(),
            ModelKeySnapshot = policy.ModelKeySnapshot,
            SubmittedAt = submittedAt,
            MaxScore = policyTasks.Sum(task => task.MaxPoints),
            TaskResults = policyTasks
                .Select(task => scoringService.CreateInitialResult(
                    task,
                    answers.GetValueOrDefault(task.Id) ?? string.Empty,
                    now))
                .ToList()
        };

        scoringService.RecalculateReview(review);
        review.Status = GetInitialStatus(review.TaskResults);
        if (review.Status == ReviewStatus.Queued)
            review.QueuedAt = now;
        else if (review.Status == ReviewStatus.Checked)
            review.CompletedAt = now;

        dbContext.Reviews.Add(review);
        await PrepareLlmReviewAsync(review, cancellationToken);
        return review;
    }

    private async Task PrepareLlmReviewAsync(
        Review review,
        CancellationToken cancellationToken)
    {
        var llmTaskCount = review.TaskResults.Count(task =>
            task.CheckMode == ReviewCheckMode.Llm
            && task.Status == ReviewTaskStatus.Pending);
        if (llmTaskCount == 0)
            return;

        if (!_processingOptions.LlmGatewayEnabled)
        {
            review.LlmQuotaReservationError =
                "Автоматическая LLM-проверка временно отключена в ReviewService.";
            return;
        }

        if (string.IsNullOrWhiteSpace(review.TeacherUserId))
        {
            review.LlmQuotaReservationError = "Для теста не указан преподаватель-владелец.";
            return;
        }

        if (string.IsNullOrWhiteSpace(review.ModelKeySnapshot))
        {
            review.LlmQuotaReservationError = "Для теста не выбрана LLM-модель проверки.";
            return;
        }

        var quota = await modelAccessService.TryConsumeChecksAsync(
            review.TeacherUserId,
            review.ModelKeySnapshot,
            llmTaskCount,
            cancellationToken);
        if (quota.Status == ModelQuotaConsumptionStatus.Allowed)
        {
            review.LlmQuotaReservedAt = DateTimeOffset.UtcNow;
            review.LlmQuotaReservationError = string.Empty;
            return;
        }

        review.LlmQuotaReservationError = CreateQuotaError(review.ModelKeySnapshot, quota);
    }

    private static ReviewStatus GetInitialStatus(IEnumerable<ReviewTask> tasks)
    {
        var taskList = tasks.ToList();
        if (taskList.Any(task => task.Status == ReviewTaskStatus.Pending))
            return ReviewStatus.Queued;
        if (taskList.Any(task => task.Status == ReviewTaskStatus.ManualReview))
            return ReviewStatus.ManualReview;
        if (taskList.Any(task => task.Status == ReviewTaskStatus.Failed))
            return ReviewStatus.Failed;
        return ReviewStatus.Checked;
    }

    internal static string CreateQuotaError(string modelKey, ModelQuotaConsumptionResult result)
    {
        return result.Status switch
        {
            ModelQuotaConsumptionStatus.AccessNotFound =>
                $"Преподавателю не выдан доступ к модели '{modelKey}'.",
            ModelQuotaConsumptionStatus.LimitExceeded =>
                $"Лимит проверок для модели '{modelKey}' исчерпан. Осталось: {result.RemainingChecks}.",
            ModelQuotaConsumptionStatus.InvalidCheckCount =>
                "Количество LLM-проверок должно быть больше нуля.",
            _ => "Не удалось зарезервировать лимит LLM-проверок."
        };
    }
}

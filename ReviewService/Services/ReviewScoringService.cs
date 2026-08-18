using System.Text.Json;
using ReviewService.Contracts.Enums;
using ReviewService.Contracts.Events;
using ReviewService.Helpers;
using ReviewService.Interfaces;
using ReviewService.Models.Reviews;

namespace ReviewService.Services;

public sealed class ReviewScoringService : IReviewScoringService
{
    public ReviewTask CreateInitialResult(
        ReviewTaskPolicySnapshot task,
        string studentAnswer,
        DateTimeOffset now)
    {
        return task.CheckMode switch
        {
            ReviewCheckMode.Auto => CreateAutoResult(task, studentAnswer, now),
            ReviewCheckMode.Manual => CreateManualResult(task, studentAnswer),
            ReviewCheckMode.Llm => CreatePendingLlmResult(task, studentAnswer),
            _ => CreateManualResult(task, studentAnswer)
        };
    }

    public void ApplyLlmResult(
        ReviewTask existingResult,
        LlmTaskReviewResult llmResult,
        DateTimeOffset now)
    {
        existingResult.Status = ReviewTaskStatus.Succeeded;
        existingResult.Score = NormalizeScore(llmResult.Score, existingResult.MaxScore);
        existingResult.Feedback = string.IsNullOrWhiteSpace(llmResult.Feedback)
            ? "Ответ проверен автоматически."
            : llmResult.Feedback.Trim();
        existingResult.FindingsJson = JsonSerializer.Serialize(
            (llmResult.Findings ?? [])
                .Where(finding => !string.IsNullOrWhiteSpace(finding))
                .Select(finding => finding.Trim())
                .Take(5)
                .DefaultIfEmpty("Ответ проверен автоматически."),
            JsonHelper.Options);
        existingResult.CompletedAt = now;
        existingResult.NextRetryAt = null;
        existingResult.LastError = string.Empty;
    }

    public void RecalculateReview(Review review)
    {
        review.Score = review.TaskResults
            .Where(result => result.Status == ReviewTaskStatus.Succeeded)
            .Sum(result => result.Score);
        review.Summary = CreateSummary(review.Score, review.MaxScore);
    }

    public ReviewStatus GetReviewStatusAfterTaskProcessing(Review review)
    {
        if (review.TaskResults.Any(result => result.Status == ReviewTaskStatus.ManualReview))
            return ReviewStatus.ManualReview;

        if (review.TaskResults.Any(result => result.Status is ReviewTaskStatus.Pending
                or ReviewTaskStatus.Processing
                or ReviewTaskStatus.RetryScheduled))
        {
            return ReviewStatus.Processing;
        }

        if (review.TaskResults.Any(result => result.Status == ReviewTaskStatus.Failed))
            return ReviewStatus.Failed;

        return ReviewStatus.Checked;
    }

    public string CreateSummary(decimal score, decimal maxScore)
    {
        var percent = maxScore == 0 ? 0 : score / maxScore;
        if (percent >= 0.8m)
            return "Работа в целом соответствует требованиям.";

        if (percent >= 0.55m)
            return "Работа частично соответствует требованиям.";

        return "Работа пока не закрывает ключевые требования.";
    }

    private static ReviewTask CreateAutoResult(
        ReviewTaskPolicySnapshot task,
        string answer,
        DateTimeOffset now)
    {
        return task.Type switch
        {
            ReviewTaskType.SingleChoice => CreateSingleChoiceResult(task, answer, now),
            ReviewTaskType.MultipleChoice => CreateMultipleChoiceResult(task, answer, now),
            _ => CreateFreeTextHeuristicResult(task, answer, now)
        };
    }

    private static ReviewTask CreateSingleChoiceResult(
        ReviewTaskPolicySnapshot task,
        string answer,
        DateTimeOffset now)
    {
        var selectedOptionIds = ParseSelectedOptions(answer);
        var correctOptionIds = task.Options
            .Where(option => option.IsCorrect)
            .Select(option => option.Id)
            .ToHashSet(StringComparer.Ordinal);
        var isCorrect = selectedOptionIds.SetEquals(correctOptionIds);

        return CreateResult(
            task,
            answer,
            ReviewTaskStatus.Succeeded,
            isCorrect ? task.MaxPoints : 0,
            isCorrect ? "Ответ выбран верно." : "Ответ не совпадает с правильным вариантом.",
            isCorrect
                ? ["Выбран корректный вариант."]
                : ["Нужно повторить материал по этому вопросу."],
            now);
    }

    private static ReviewTask CreateMultipleChoiceResult(
        ReviewTaskPolicySnapshot task,
        string answer,
        DateTimeOffset now)
    {
        var selectedOptionIds = ParseSelectedOptions(answer);
        var correctOptionIds = task.Options
            .Where(option => option.IsCorrect)
            .Select(option => option.Id)
            .ToHashSet(StringComparer.Ordinal);

        var selectedCorrectCount = selectedOptionIds.Count(correctOptionIds.Contains);
        var selectedWrongCount = selectedOptionIds.Count(optionId => !correctOptionIds.Contains(optionId));
        var pointsPerCorrectOption = correctOptionIds.Count == 0
            ? 0
            : task.MaxPoints / correctOptionIds.Count;
        var score = NormalizeScore(
            selectedCorrectCount * pointsPerCorrectOption
                - selectedWrongCount * task.WrongAnswerPenalty,
            task.MaxPoints);

        return CreateResult(
            task,
            answer,
            ReviewTaskStatus.Succeeded,
            score,
            selectedWrongCount == 0 && selectedCorrectCount == correctOptionIds.Count
                ? "Все правильные варианты выбраны."
                : "Баллы начислены за правильные варианты с учетом штрафа за неверные.",
            [
                $"Правильных вариантов выбрано: {selectedCorrectCount}.",
                $"Неверных вариантов выбрано: {selectedWrongCount}."
            ],
            now);
    }

    private static ReviewTask CreateFreeTextHeuristicResult(
        ReviewTaskPolicySnapshot task,
        string answer,
        DateTimeOffset now)
    {
        var normalizedAnswer = answer.Trim();
        var score = 0m;
        var findings = new List<string>();

        if (normalizedAnswer.Length > 120)
        {
            score += task.MaxPoints * 0.6m;
            findings.Add("Ответ содержит развернутое объяснение.");
        }
        else if (normalizedAnswer.Length > 40)
        {
            score += task.MaxPoints * 0.35m;
            findings.Add("Ответ содержит базовую аргументацию.");
        }
        else
        {
            findings.Add("Ответ слишком короткий для уверенной проверки.");
        }

        score = NormalizeScore(score, task.MaxPoints);
        return CreateResult(
            task,
            answer,
            ReviewTaskStatus.Succeeded,
            score,
            score >= task.MaxPoints * 0.75m
                ? "Ответ выглядит достаточно полным."
                : "Ответ требует доработки: добавь ход рассуждения и обоснование вывода.",
            findings,
            now);
    }

    private static ReviewTask CreatePendingLlmResult(
        ReviewTaskPolicySnapshot task,
        string answer)
    {
        return CreateResult(
            task,
            answer,
            ReviewTaskStatus.Pending,
            0,
            string.Empty,
            [],
            completedAt: null);
    }

    private static ReviewTask CreateManualResult(
        ReviewTaskPolicySnapshot task,
        string answer)
    {
        return CreateResult(
            task,
            answer,
            ReviewTaskStatus.ManualReview,
            0,
            "Ожидает ручной проверки.",
            ["Задание ожидает ручной проверки преподавателем."],
            completedAt: null);
    }

    private static ReviewTask CreateResult(
        ReviewTaskPolicySnapshot task,
        string answer,
        ReviewTaskStatus status,
        decimal score,
        string feedback,
        List<string> findings,
        DateTimeOffset? completedAt)
    {
        return new ReviewTask
        {
            TaskId = task.Id,
            TaskType = task.Type,
            TaskTitle = task.Title,
            TaskPrompt = task.Prompt,
            CheckMode = task.CheckMode,
            Status = status,
            StudentAnswer = answer,
            WrongAnswerPenalty = task.WrongAnswerPenalty,
            AnswerOptionsJson = JsonSerializer.Serialize(task.Options, JsonHelper.Options),
            Score = score,
            MaxScore = task.MaxPoints,
            Feedback = feedback,
            FindingsJson = JsonSerializer.Serialize(findings, JsonHelper.Options),
            CompletedAt = completedAt
        };
    }

    private static HashSet<string> ParseSelectedOptions(string answer)
    {
        return answer
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static decimal NormalizeScore(decimal score, decimal maxScore)
    {
        return Math.Clamp(Math.Round(score, 1), 0, maxScore);
    }
}

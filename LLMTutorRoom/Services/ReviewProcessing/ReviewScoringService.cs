using System.Text.Json;
using LLMTutorRoom.Models;
using TeachingService.Contracts.Enums;
using TeachingService.Contracts.Models;

namespace LLMTutorRoom.Services.ReviewProcessing
{
    public sealed class ReviewScoringService
    {
        public TaskReviewResult CreateInitialResult(TestTaskDto task, string answer)
        {
            return task.CheckMode switch
            {
                TestTaskCheckMode.Auto => CreateAutoResult(task, answer),
                TestTaskCheckMode.Manual => CreateManualResult(task),
                TestTaskCheckMode.Llm => CreatePendingLlmResult(task),
                _ => CreateManualResult(task)
            };
        }

        public TaskReviewResult CreateLlmResult(
            TaskReviewResult existingResult,
            LlmTaskReviewResult llmResult)
        {
            existingResult.Status = TaskReviewResultStatus.Succeeded;
            existingResult.Score = NormalizeScore(llmResult.Score, existingResult.MaxScore);
            existingResult.Feedback = string.IsNullOrWhiteSpace(llmResult.Feedback)
                ? "Ответ проверен автоматически."
                : llmResult.Feedback.Trim();
            existingResult.Findings = llmResult.Findings
                .Where(finding => !string.IsNullOrWhiteSpace(finding))
                .Select(finding => finding.Trim())
                .Take(5)
                .DefaultIfEmpty("Ответ проверен автоматически.")
                .ToList();
            existingResult.CompletedAt = DateTimeOffset.UtcNow;
            existingResult.NextRetryAt = null;
            existingResult.LastError = string.Empty;
            return existingResult;
        }

        public void RecalculateReview(SubmissionReview review)
        {
            review.Score = review.TaskResults
                .Where(result => result.Status == TaskReviewResultStatus.Succeeded)
                .Sum(result => result.Score);
            review.Summary = CreateSummary(review.Score, review.MaxScore);
        }

        public SubmissionReviewStatus GetReviewStatusAfterTaskProcessing(SubmissionReview review)
        {
            if (review.TaskResults.Any(result => result.Status == TaskReviewResultStatus.ManualReview))
                return SubmissionReviewStatus.ManualReview;

            if (review.TaskResults.Any(result =>
                    result.Status == TaskReviewResultStatus.Pending
                    || result.Status == TaskReviewResultStatus.Processing
                    || result.Status == TaskReviewResultStatus.RetryScheduled))
            {
                return SubmissionReviewStatus.Processing;
            }

            if (review.TaskResults.Any(result => result.Status == TaskReviewResultStatus.Failed))
                return SubmissionReviewStatus.Failed;

            return SubmissionReviewStatus.Checked;
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

        private static TaskReviewResult CreateAutoResult(TestTaskDto task, string answer)
        {
            if (task.Type == TestTaskType.SingleChoice)
                return CreateSingleChoiceTaskReview(task, answer);

            if (task.Type == TestTaskType.MultipleChoice)
                return CreateMultipleChoiceTaskReview(task, answer);

            return CreateFreeTextHeuristicTaskReview(task, answer);
        }

        private static TaskReviewResult CreateSingleChoiceTaskReview(TestTaskDto task, string answer)
        {
            var selectedOptionIds = answer
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet();
            var correctOptionIds = task.CorrectOptionIds.ToHashSet();
            var isCorrect = selectedOptionIds.SetEquals(correctOptionIds);

            return new TaskReviewResult
            {
                TaskId = task.Id,
                TaskTitle = task.Title,
                TaskPrompt = task.Prompt,
                CheckMode = TestTaskCheckMode.Auto,
                Status = TaskReviewResultStatus.Succeeded,
                Score = isCorrect ? task.MaxPoints : 0,
                MaxScore = task.MaxPoints,
                Feedback = isCorrect
                    ? "Ответ выбран верно."
                    : "Ответ не совпадает с правильным вариантом.",
                CompletedAt = DateTimeOffset.UtcNow,
                Findings = isCorrect
                    ? new List<string> { "Выбран корректный вариант." }
                    : new List<string> { "Нужно повторить материал по этому вопросу." }
            };
        }

        private static TaskReviewResult CreateMultipleChoiceTaskReview(TestTaskDto task, string answer)
        {
            var selectedOptionIds = answer
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet();
            var correctOptionIds = task.CorrectOptionIds.ToHashSet();
            var selectedCorrectCount = selectedOptionIds.Count(correctOptionIds.Contains);
            var selectedWrongCount = selectedOptionIds.Count(optionId => !correctOptionIds.Contains(optionId));
            var pointsPerCorrectOption = correctOptionIds.Count == 0
                ? 0
                : task.MaxPoints / correctOptionIds.Count;
            var score = selectedCorrectCount * pointsPerCorrectOption
                - selectedWrongCount * task.WrongAnswerPenalty;

            score = NormalizeScore(score, task.MaxPoints);

            return new TaskReviewResult
            {
                TaskId = task.Id,
                TaskTitle = task.Title,
                TaskPrompt = task.Prompt,
                CheckMode = TestTaskCheckMode.Auto,
                Status = TaskReviewResultStatus.Succeeded,
                Score = score,
                MaxScore = task.MaxPoints,
                Feedback = selectedWrongCount == 0 && selectedCorrectCount == correctOptionIds.Count
                    ? "Все правильные варианты выбраны."
                    : "Баллы начислены за правильные варианты с учетом штрафа за неверные.",
                CompletedAt = DateTimeOffset.UtcNow,
                Findings = new List<string>
                {
                    $"Правильных вариантов выбрано: {selectedCorrectCount}.",
                    $"Неверных вариантов выбрано: {selectedWrongCount}."
                }
            };
        }

        private static TaskReviewResult CreateFreeTextHeuristicTaskReview(TestTaskDto task, string answer)
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

            return new TaskReviewResult
            {
                TaskId = task.Id,
                TaskTitle = task.Title,
                TaskPrompt = task.Prompt,
                CheckMode = TestTaskCheckMode.Auto,
                Status = TaskReviewResultStatus.Succeeded,
                Score = score,
                MaxScore = task.MaxPoints,
                Feedback = score >= task.MaxPoints * 0.75m
                    ? "Ответ выглядит достаточно полным."
                    : "Ответ требует доработки: добавь ход рассуждения и обоснование вывода.",
                CompletedAt = DateTimeOffset.UtcNow,
                Findings = findings
            };
        }

        private static TaskReviewResult CreatePendingLlmResult(TestTaskDto task)
        {
            return new TaskReviewResult
            {
                TaskId = task.Id,
                TaskTitle = task.Title,
                TaskPrompt = task.Prompt,
                CheckMode = TestTaskCheckMode.Llm,
                Status = TaskReviewResultStatus.Pending,
                Score = 0,
                MaxScore = task.MaxPoints,
                Feedback = string.Empty,
                Findings = new List<string>()
            };
        }

        private static TaskReviewResult CreateManualResult(TestTaskDto task)
        {
            return new TaskReviewResult
            {
                TaskId = task.Id,
                TaskTitle = task.Title,
                TaskPrompt = task.Prompt,
                CheckMode = TestTaskCheckMode.Manual,
                Status = TaskReviewResultStatus.ManualReview,
                Score = 0,
                MaxScore = task.MaxPoints,
                Feedback = "Ожидает ручной проверки.",
                Findings = new List<string> { "Задание ожидает ручной проверки преподавателем." }
            };
        }

        private static decimal NormalizeScore(decimal score, decimal maxScore)
        {
            return Math.Clamp(Math.Round(score, 1), 0, maxScore);
        }
    }

    public sealed class LlmTaskReviewResult
    {
        public decimal Score { get; set; }
        public string Feedback { get; set; } = string.Empty;
        public List<string> Findings { get; set; } = new();
    }
}

using TeachingService.Contracts.Models;
using GradingExampleModel = TeachingService.Models.GradingExample;

namespace TeachingService.Helpers;

public static class GradingExamples
{
    public const int MaxCount = 5;
    public const int MaxPromptLength = 4000;
    public const int MaxAnswerLength = 8000;
    public const int MaxFeedbackLength = 4000;

    public static string? Validate(IReadOnlyList<GradingExampleDto>? examples, bool requireTaskPrompt)
    {
        if (examples is null)
            return "GradingExamples must be an array.";
        if (examples.Count > MaxCount)
            return $"At most {MaxCount} grading examples are allowed at each level.";

        foreach (var example in examples)
        {
            if (example is null
                || (requireTaskPrompt && string.IsNullOrWhiteSpace(example.TaskPrompt))
                || string.IsNullOrWhiteSpace(example.StudentAnswer)
                || string.IsNullOrWhiteSpace(example.Feedback))
                return "Each grading example requires an answer and feedback; test-level examples also require a task prompt.";

            if (example.MaxScore <= 0 || example.Score < 0 || example.Score > example.MaxScore)
                return "Example MaxScore must be positive and Score must be between zero and MaxScore.";

            if ((example.TaskPrompt?.Length ?? 0) > MaxPromptLength
                || example.StudentAnswer.Length > MaxAnswerLength
                || example.Feedback.Length > MaxFeedbackLength)
                return $"Example prompt, answer and feedback must not exceed {MaxPromptLength}, {MaxAnswerLength} and {MaxFeedbackLength} characters respectively.";
        }

        return null;
    }

    public static List<GradingExampleModel> ToModels(IEnumerable<GradingExampleDto> examples)
    {
        return examples.Select(example => new GradingExampleModel
        {
            TaskPrompt = example.TaskPrompt?.Trim() ?? string.Empty,
            StudentAnswer = example.StudentAnswer.Trim(),
            Score = example.Score,
            MaxScore = example.MaxScore,
            Feedback = example.Feedback.Trim()
        }).ToList();
    }

    public static List<GradingExampleDto> ToDtos(IEnumerable<GradingExampleModel> examples)
    {
        return examples.Select(example => new GradingExampleDto
        {
            TaskPrompt = example.TaskPrompt,
            StudentAnswer = example.StudentAnswer,
            Score = example.Score,
            MaxScore = example.MaxScore,
            Feedback = example.Feedback
        }).ToList();
    }

    public static List<GradingExampleModel> Clone(IEnumerable<GradingExampleModel> examples)
    {
        return examples.Select(example => new GradingExampleModel
        {
            TaskPrompt = example.TaskPrompt,
            StudentAnswer = example.StudentAnswer,
            Score = example.Score,
            MaxScore = example.MaxScore,
            Feedback = example.Feedback
        }).ToList();
    }
}

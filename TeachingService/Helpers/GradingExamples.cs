using System.Text.Json;
using TeachingService.Contracts.Models;

namespace TeachingService.Helpers;

public static class GradingExamples
{
    public const int MaxCount = 5;
    public const int MaxPromptLength = 4000;
    public const int MaxAnswerLength = 8000;
    public const int MaxFeedbackLength = 4000;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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

    public static string Serialize(IEnumerable<GradingExampleDto> examples)
    {
        return JsonSerializer.Serialize(examples.Select(example => example with
        {
            TaskPrompt = example.TaskPrompt?.Trim() ?? string.Empty,
            StudentAnswer = example.StudentAnswer.Trim(),
            Feedback = example.Feedback.Trim()
        }), JsonOptions);
    }

    public static List<GradingExampleDto> Deserialize(string json)
    {
        return JsonSerializer.Deserialize<List<GradingExampleDto>>(json, JsonOptions) ?? [];
    }
}

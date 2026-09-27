namespace TeachingService.Contracts.Models;

/// <summary>A teacher-graded answer used as an example during LLM review.</summary>
public sealed record GradingExampleDto
{
    public string TaskPrompt { get; init; } = string.Empty;
    public string StudentAnswer { get; init; } = string.Empty;
    public decimal Score { get; init; }
    public decimal MaxScore { get; init; }
    public string Feedback { get; init; } = string.Empty;
}

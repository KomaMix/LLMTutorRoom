namespace ReviewService.Contracts.Events;

/// <summary>A teacher-graded answer captured in an immutable review policy.</summary>
public sealed record GradingExampleSnapshot
{
    public string TaskPrompt { get; init; } = string.Empty;
    public string StudentAnswer { get; init; } = string.Empty;
    public decimal Score { get; init; }
    public decimal MaxScore { get; init; }
    public string Feedback { get; init; } = string.Empty;
}

using ReviewService.Contracts.Enums;

namespace ReviewService.Models.Reviews;

public sealed class ReviewTask
{
    public int Id { get; set; }
    public int ReviewId { get; set; }
    public string TaskId { get; set; } = string.Empty;
    public ReviewTaskType TaskType { get; set; }
    public string TaskTitle { get; set; } = string.Empty;
    public string TaskPrompt { get; set; } = string.Empty;
    public ReviewCheckMode CheckMode { get; set; }
    public ReviewTaskStatus Status { get; set; }
    public string StudentAnswer { get; set; } = string.Empty;
    public decimal WrongAnswerPenalty { get; set; }
    public string AnswerOptionsJson { get; set; } = "[]";
    public int Attempts { get; set; }
    public DateTimeOffset? NextRetryAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string LastError { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public decimal MaxScore { get; set; }
    public string Feedback { get; set; } = string.Empty;
    public string FindingsJson { get; set; } = "[]";
}

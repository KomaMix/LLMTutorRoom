using ReviewService.Contracts.Enums;

namespace ReviewService.Contracts.Responses;

public class ReviewTaskResponse
{
    public int Id { get; set; }
    public string TaskId { get; set; } = string.Empty;
    public string TaskTitle { get; set; } = string.Empty;
    public string TaskPrompt { get; set; } = string.Empty;
    public string StudentAnswer { get; set; } = string.Empty;
    public List<ReviewAnswerOptionResponse> AnswerOptions { get; set; } = new();
    public ReviewCheckMode CheckMode { get; set; }
    public ReviewTaskStatus Status { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset? NextRetryAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string LastError { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public decimal MaxScore { get; set; }
    public string Feedback { get; set; } = string.Empty;
    public List<string> Findings { get; set; } = new();
    public List<FeedbackSectionResponse> FeedbackSections { get; set; } = new();
    public LlmExecutionResponse? LlmExecution { get; set; }
}

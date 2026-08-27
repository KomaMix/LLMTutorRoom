using ReviewService.Contracts.Enums;

namespace ReviewService.Contracts.Responses;

public sealed record ReviewTaskResponse(
    int Id,
    string TaskId,
    string TaskTitle,
    string TaskPrompt,
    string StudentAnswer,
    List<ReviewAnswerOptionResponse> AnswerOptions,
    ReviewCheckMode CheckMode,
    ReviewTaskStatus Status,
    int Attempts,
    DateTimeOffset? NextRetryAt,
    DateTimeOffset? CompletedAt,
    string LastError,
    decimal Score,
    decimal MaxScore,
    string Feedback,
    List<string> Findings);

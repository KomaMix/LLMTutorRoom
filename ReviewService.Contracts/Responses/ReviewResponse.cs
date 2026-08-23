using ReviewService.Contracts.Enums;

namespace ReviewService.Contracts.Responses;

public sealed record ReviewResponse(
    int Id,
    Guid AttemptId,
    string TestId,
    int TestRevision,
    string TestTitle,
    string TeacherUserId,
    string StudentUserId,
    string? StudentName,
    ReviewStatus Status,
    string ModelKey,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? QueuedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? NextRetryAt,
    int ProcessingAttempts,
    string LastError,
    decimal Score,
    decimal MaxScore,
    string Summary,
    List<ReviewTaskResponse> TaskResults);

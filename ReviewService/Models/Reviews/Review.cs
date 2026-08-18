using ReviewService.Contracts.Enums;

namespace ReviewService.Models.Reviews;

public sealed class Review
{
    public int Id { get; set; }
    public int AttemptId { get; set; }
    public string TestId { get; set; } = string.Empty;
    public int TestRevision { get; set; }
    public string TestTitle { get; set; } = string.Empty;
    public string TeacherUserId { get; set; } = string.Empty;
    public string StudentUserId { get; set; } = string.Empty;
    public string? StudentName { get; set; }
    public ReviewStatus Status { get; set; } = ReviewStatus.Checked;
    public string ModelKeySnapshot { get; set; } = string.Empty;
    public DateTimeOffset SubmittedAt { get; set; }
    public DateTimeOffset? QueuedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? NextRetryAt { get; set; }
    public DateTimeOffset? ProcessingLeaseExpiresAt { get; set; }
    public DateTimeOffset? LastEnqueuedAt { get; set; }
    public DateTimeOffset? LlmQuotaReservedAt { get; set; }
    public string LlmQuotaReservationError { get; set; } = string.Empty;
    public int ProcessingAttempts { get; set; }
    public int ProcessingGeneration { get; set; }
    public string LastError { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public decimal MaxScore { get; set; }
    public string Summary { get; set; } = string.Empty;
    public List<ReviewTask> TaskResults { get; set; } = [];
}

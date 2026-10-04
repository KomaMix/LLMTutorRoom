using ReviewService.Contracts.Enums;

namespace ReviewService.Contracts.Responses;

public class ReviewResponse
{
    public int Id { get; set; }
    public Guid AttemptId { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid GroupId { get; set; }
    public string TestId { get; set; } = string.Empty;
    public int TestRevision { get; set; }
    public string TestTitle { get; set; } = string.Empty;
    public string TeacherUserId { get; set; } = string.Empty;
    public string StudentUserId { get; set; } = string.Empty;
    public string? StudentName { get; set; }
    public ReviewStatus Status { get; set; }
    public int? ModelId { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public DateTimeOffset? QueuedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? NextRetryAt { get; set; }
    public int ProcessingAttempts { get; set; }
    public string LastError { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public decimal MaxScore { get; set; }
    public string Summary { get; set; } = string.Empty;
    public IList<ReviewTaskResponse> TaskResults { get; set; } = new List<ReviewTaskResponse>();
}

namespace ReviewService.Models.Reviews;

public sealed class PendingSubmission
{
    public int Id { get; set; }
    public Guid AttemptId { get; set; }
    public string TestId { get; set; } = string.Empty;
    public int TestRevision { get; set; }
    public string StudentUserId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string AnswersJson { get; set; } = "{}";
    public DateTimeOffset SubmittedAt { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
}

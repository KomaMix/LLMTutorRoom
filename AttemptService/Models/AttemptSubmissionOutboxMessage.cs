namespace AttemptService.Models;

public class AttemptSubmissionOutboxMessage
{
    public Guid Id { get; set; }
    public Guid AttemptId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public string? TraceParent { get; set; }
    public string? TraceState { get; set; }
    public int PublishAttempts { get; set; }
    public DateTimeOffset? NextPublishAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public string LastError { get; set; } = string.Empty;
}

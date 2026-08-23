namespace AttemptService.Models;

public sealed class AttemptSubmissionOutboxMessage
{
    public Guid Id { get; set; }
    public Guid AttemptId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public int PublishAttempts { get; set; }
    public DateTimeOffset? NextPublishAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public string LastError { get; set; } = string.Empty;
}

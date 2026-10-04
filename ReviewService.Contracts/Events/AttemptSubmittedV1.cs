namespace ReviewService.Contracts.Events;

public class AttemptSubmittedV1
{
    public Guid EventId { get; set; }
    public Guid AttemptId { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid GroupId { get; set; }
    public string TestId { get; set; } = string.Empty;
    public int TestRevision { get; set; }
    public string StudentUserId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public Dictionary<string, string> Answers { get; set; } = new();
    public DateTimeOffset SubmittedAt { get; set; }
}

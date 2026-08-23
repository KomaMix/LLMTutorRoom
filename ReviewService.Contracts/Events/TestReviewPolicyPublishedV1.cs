namespace ReviewService.Contracts.Events;

public sealed class TestReviewPolicyPublishedV1
{
    public Guid EventId { get; set; }
    public string TestId { get; set; } = string.Empty;
    public int Revision { get; set; }
    public string TeacherUserId { get; set; } = string.Empty;
    public string TestTitle { get; set; } = string.Empty;
    public string ModelKey { get; set; } = string.Empty;
    public List<ReviewTaskPolicySnapshot> Tasks { get; set; } = new();
    public DateTimeOffset PublishedAt { get; set; }
}

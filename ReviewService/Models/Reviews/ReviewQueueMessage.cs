namespace ReviewService.Models.Reviews;

public sealed class ReviewQueueMessage
{
    public int ReviewId { get; set; }
    public Guid AttemptId { get; set; }
    public string ModelKey { get; set; } = string.Empty;
    public DateTimeOffset RequestedAt { get; set; }
}

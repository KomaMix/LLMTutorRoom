namespace ReviewService.Models.Reviews;

public sealed class ReviewQueueMessage
{
    public int ReviewId { get; set; }
    public Guid AttemptId { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
}

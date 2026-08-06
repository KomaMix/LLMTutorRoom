namespace LLMTutorRoom.Services.ReviewProcessing
{
    public sealed class ReviewQueueMessage
    {
        public int ReviewId { get; set; }
        public int AttemptId { get; set; }
        public string ModelKey { get; set; } = string.Empty;
        public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}

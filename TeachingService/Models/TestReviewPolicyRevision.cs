namespace TeachingService.Models
{
    public sealed class TestReviewPolicyRevision
    {
        public Guid TestId { get; set; }
        public int Revision { get; set; }
        public Guid EventId { get; set; }
        public string Payload { get; set; } = string.Empty;
        public DateTimeOffset PublishedAt { get; set; }
    }
}

namespace TeachingService.Models
{
    public sealed class IntegrationOutboxMessage
    {
        private IntegrationOutboxMessage()
        {
        }

        public IntegrationOutboxMessage(
            Guid id,
            string eventType,
            string routingKey,
            string payload,
            DateTimeOffset occurredAt)
        {
            Id = id;
            EventType = eventType;
            RoutingKey = routingKey;
            Payload = payload;
            OccurredAt = occurredAt;
            NextPublishAttemptAt = occurredAt;
        }

        public Guid Id { get; private set; }
        public string EventType { get; private set; } = string.Empty;
        public string RoutingKey { get; private set; } = string.Empty;
        public string Payload { get; private set; } = string.Empty;
        public DateTimeOffset OccurredAt { get; private set; }
        public DateTimeOffset NextPublishAttemptAt { get; private set; }
        public DateTimeOffset? PublishedAt { get; private set; }
        public int PublishAttempts { get; private set; }
        public string? LastError { get; private set; }

        public void MarkPublished(DateTimeOffset publishedAt)
        {
            PublishedAt = publishedAt;
            LastError = null;
        }

        public void MarkPublishFailed(
            DateTimeOffset nextPublishAttemptAt,
            string error)
        {
            PublishAttempts++;
            NextPublishAttemptAt = nextPublishAttemptAt;
            LastError = error;
        }
    }
}

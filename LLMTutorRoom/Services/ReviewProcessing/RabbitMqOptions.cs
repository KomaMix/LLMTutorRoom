namespace LLMTutorRoom.Services.ReviewProcessing
{
    public sealed class RabbitMqOptions
    {
        public bool Enabled { get; set; } = true;
        public string HostName { get; set; } = "localhost";
        public int Port { get; set; } = 5672;
        public string VirtualHost { get; set; } = "/";
        public string UserName { get; set; } = "llm";
        public string Password { get; set; } = "llm-dev";
        public string ExchangeName { get; set; } = "llmtutorroom.review";
        public string QueueName { get; set; } = "llmtutorroom.review.check";
        public string RoutingKey { get; set; } = "review.check";
        public string RetryExchangeName { get; set; } = "llmtutorroom.review.retry";
        public string RetryQueueNamePrefix { get; set; } = "llmtutorroom.review.retry";
        public string RetryRoutingKeyPrefix { get; set; } = "review.check.retry";
        public string DeadLetterExchangeName { get; set; } = "llmtutorroom.review.dead";
        public string DeadLetterQueueName { get; set; } = "llmtutorroom.review.dlq";
        public string DeadLetterRoutingKey { get; set; } = "review.check.dead";
        public ushort PrefetchCount { get; set; } = 1;
        public int ConnectionRetryDelaySeconds { get; set; } = 5;
        public int[] RetryDelaysSeconds { get; set; } = { 30, 120, 600, 1800 };
    }
}

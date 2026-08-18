namespace ReviewService.Options;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public bool Enabled { get; set; } = true;
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string UserName { get; set; } = "llm";
    public string Password { get; set; } = "llm-dev";
    public string IntegrationQueueName { get; set; } = "review-service.integration";
    public string IntegrationRetryExchangeName { get; set; } = "review.integration.retry";
    public string IntegrationRetryQueueName { get; set; } = "review-service.integration.retry";
    public int IntegrationRetryDelaySeconds { get; set; } = 15;
    public int IntegrationMaxRetryAttempts { get; set; } = 5;
    public string IntegrationDeadLetterExchangeName { get; set; } = "review.integration.dead";
    public string IntegrationDeadLetterQueueName { get; set; } = "review-service.integration.dlq";
    public string IntegrationDeadLetterRoutingKey { get; set; } = "review.integration.dead";
    public string WorkExchangeName { get; set; } = "review.processing";
    public string WorkQueueName { get; set; } = "review.processing.check";
    public string WorkRoutingKey { get; set; } = "review.check";
    public string RetryExchangeName { get; set; } = "review.processing.retry";
    public string RetryQueueNamePrefix { get; set; } = "review.processing.retry";
    public string RetryRoutingKeyPrefix { get; set; } = "review.check.retry";
    public string DeadLetterExchangeName { get; set; } = "review.processing.dead";
    public string DeadLetterQueueName { get; set; } = "review.processing.dlq";
    public string DeadLetterRoutingKey { get; set; } = "review.check.dead";
    public ushort PrefetchCount { get; set; } = 1;
    public int ConnectionRetryDelaySeconds { get; set; } = 5;
    public int[] RetryDelaysSeconds { get; set; } = [];
}

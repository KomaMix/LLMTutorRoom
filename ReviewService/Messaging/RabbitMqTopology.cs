using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using ReviewService.Options;
using ReviewService.Contracts.Messaging;

namespace ReviewService.Messaging;

public sealed class RabbitMqTopology(IOptions<RabbitMqOptions> options)
{
    private readonly RabbitMqOptions _options = options.Value;

    public async Task DeclareIntegrationAsync(
        IChannel channel,
        CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            ReviewIntegrationRoutes.ExchangeName,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(
            _options.IntegrationRetryExchangeName,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(
            _options.IntegrationDeadLetterExchangeName,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            _options.IntegrationQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = _options.IntegrationDeadLetterExchangeName,
                ["x-dead-letter-routing-key"] = _options.IntegrationDeadLetterRoutingKey
            },
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            _options.IntegrationQueueName,
            ReviewIntegrationRoutes.ExchangeName,
            ReviewIntegrationRoutes.TestReviewPolicyPublishedV1,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            _options.IntegrationQueueName,
            ReviewIntegrationRoutes.ExchangeName,
            ReviewIntegrationRoutes.AttemptSubmittedV1,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            _options.IntegrationRetryQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-message-ttl"] = _options.IntegrationRetryDelaySeconds * 1000,
                ["x-dead-letter-exchange"] = ReviewIntegrationRoutes.ExchangeName
            },
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            _options.IntegrationRetryQueueName,
            _options.IntegrationRetryExchangeName,
            ReviewIntegrationRoutes.TestReviewPolicyPublishedV1,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            _options.IntegrationRetryQueueName,
            _options.IntegrationRetryExchangeName,
            ReviewIntegrationRoutes.AttemptSubmittedV1,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            _options.IntegrationDeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            _options.IntegrationDeadLetterQueueName,
            _options.IntegrationDeadLetterExchangeName,
            _options.IntegrationDeadLetterRoutingKey,
            cancellationToken: cancellationToken);
    }

    public async Task DeclareWorkAsync(
        IChannel channel,
        CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            _options.WorkExchangeName,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(
            _options.RetryExchangeName,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(
            _options.DeadLetterExchangeName,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            _options.WorkQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = _options.DeadLetterExchangeName,
                ["x-dead-letter-routing-key"] = _options.DeadLetterRoutingKey
            },
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            _options.WorkQueueName,
            _options.WorkExchangeName,
            _options.WorkRoutingKey,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            _options.DeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            _options.DeadLetterQueueName,
            _options.DeadLetterExchangeName,
            _options.DeadLetterRoutingKey,
            cancellationToken: cancellationToken);

        foreach (var delaySeconds in GetRetryDelays())
        {
            await channel.QueueDeclareAsync(
                GetRetryQueueName(delaySeconds),
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    ["x-message-ttl"] = delaySeconds * 1000,
                    ["x-dead-letter-exchange"] = _options.WorkExchangeName,
                    ["x-dead-letter-routing-key"] = _options.WorkRoutingKey
                },
                cancellationToken: cancellationToken);
            await channel.QueueBindAsync(
                GetRetryQueueName(delaySeconds),
                _options.RetryExchangeName,
                GetRetryRoutingKey(delaySeconds),
                cancellationToken: cancellationToken);
        }
    }

    public int GetRetryDelaySeconds(int attempts)
    {
        var delays = GetRetryDelays();
        return delays[Math.Clamp(attempts - 1, 0, delays.Count - 1)];
    }

    public string GetRetryRoutingKey(int delaySeconds) =>
        $"{_options.RetryRoutingKeyPrefix}.{delaySeconds}s";

    private string GetRetryQueueName(int delaySeconds) =>
        $"{_options.RetryQueueNamePrefix}.{delaySeconds}s";

    private List<int> GetRetryDelays()
    {
        return _options.RetryDelaysSeconds
            .Where(delay => delay > 0)
            .Distinct()
            .OrderBy(delay => delay)
            .DefaultIfEmpty(30)
            .ToList();
    }
}

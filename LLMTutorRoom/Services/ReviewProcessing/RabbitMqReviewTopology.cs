using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace LLMTutorRoom.Services.ReviewProcessing
{
    public sealed class RabbitMqReviewTopology
    {
        private readonly RabbitMqOptions _options;

        public RabbitMqReviewTopology(IOptions<RabbitMqOptions> options)
        {
            _options = options.Value;
        }

        public async Task DeclareAsync(IChannel channel, CancellationToken cancellationToken)
        {
            await channel.ExchangeDeclareAsync(
                _options.ExchangeName,
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

            var mainQueueArguments = new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = _options.DeadLetterExchangeName,
                ["x-dead-letter-routing-key"] = _options.DeadLetterRoutingKey
            };

            await channel.QueueDeclareAsync(
                _options.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: mainQueueArguments,
                cancellationToken: cancellationToken);

            await channel.QueueBindAsync(
                _options.QueueName,
                _options.ExchangeName,
                _options.RoutingKey,
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
                var retryQueueArguments = new Dictionary<string, object?>
                {
                    ["x-message-ttl"] = delaySeconds * 1000,
                    ["x-dead-letter-exchange"] = _options.ExchangeName,
                    ["x-dead-letter-routing-key"] = _options.RoutingKey
                };

                await channel.QueueDeclareAsync(
                    GetRetryQueueName(delaySeconds),
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: retryQueueArguments,
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
            var index = Math.Clamp(attempts - 1, 0, delays.Count - 1);
            return delays[index];
        }

        public string GetRetryRoutingKey(int delaySeconds)
        {
            return $"{_options.RetryRoutingKeyPrefix}.{delaySeconds}s";
        }

        private string GetRetryQueueName(int delaySeconds)
        {
            return $"{_options.RetryQueueNamePrefix}.{delaySeconds}s";
        }

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
}

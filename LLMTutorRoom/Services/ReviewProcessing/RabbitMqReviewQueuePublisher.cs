using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace LLMTutorRoom.Services.ReviewProcessing
{
    public sealed class RabbitMqReviewQueuePublisher : IReviewQueuePublisher
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly RabbitMqOptions _options;
        private readonly RabbitMqConnectionProvider _connectionProvider;
        private readonly RabbitMqReviewTopology _topology;

        public RabbitMqReviewQueuePublisher(
            IOptions<RabbitMqOptions> options,
            RabbitMqConnectionProvider connectionProvider,
            RabbitMqReviewTopology topology)
        {
            _options = options.Value;
            _connectionProvider = connectionProvider;
            _topology = topology;
        }

        public async Task PublishAsync(
            ReviewQueueMessage message,
            int? retryDelaySeconds,
            CancellationToken cancellationToken)
        {
            if (!_options.Enabled)
                return;

            var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);
            await using var channel = await connection.CreateChannelAsync(
                cancellationToken: cancellationToken);
            await _topology.DeclareAsync(channel, cancellationToken);

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, JsonOptions));
            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                MessageId = $"review-{message.ReviewId}",
                Type = "review-requested",
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };

            var exchange = retryDelaySeconds.HasValue
                ? _options.RetryExchangeName
                : _options.ExchangeName;
            var routingKey = retryDelaySeconds.HasValue
                ? _topology.GetRetryRoutingKey(retryDelaySeconds.Value)
                : _options.RoutingKey;

            await channel.BasicPublishAsync(
                exchange,
                routingKey,
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);
        }
    }
}

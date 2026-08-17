using System.Text;
using RabbitMQ.Client;
using ReviewService.Contracts.Messaging;
using TeachingService.Interfaces;
using TeachingService.Models;

namespace TeachingService.Services.IntegrationEvents
{
    public sealed class RabbitMqIntegrationEventPublisher : IIntegrationEventPublisher
    {
        private readonly RabbitMqIntegrationConnectionProvider _connectionProvider;

        public RabbitMqIntegrationEventPublisher(
            RabbitMqIntegrationConnectionProvider connectionProvider)
        {
            _connectionProvider = connectionProvider;
        }

        public async Task PublishAsync(
            IntegrationOutboxMessage message,
            CancellationToken cancellationToken)
        {
            var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);
            var channelOptions = new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true);
            await using var channel = await connection.CreateChannelAsync(
                channelOptions,
                cancellationToken);

            await channel.ExchangeDeclareAsync(
                ReviewIntegrationRoutes.ExchangeName,
                ExchangeType.Direct,
                durable: true,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);

            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                ContentEncoding = "utf-8",
                MessageId = message.Id.ToString(),
                Type = message.EventType,
                Timestamp = new AmqpTimestamp(message.OccurredAt.ToUnixTimeSeconds())
            };

            await channel.BasicPublishAsync(
                ReviewIntegrationRoutes.ExchangeName,
                message.RoutingKey,
                mandatory: true,
                basicProperties: properties,
                body: Encoding.UTF8.GetBytes(message.Payload),
                cancellationToken: cancellationToken);
        }
    }
}

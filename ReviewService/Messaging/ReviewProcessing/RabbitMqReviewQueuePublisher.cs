using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using ReviewService.Helpers;
using ReviewService.Interfaces;
using ReviewService.Messaging;
using ReviewService.Models.Reviews;
using ReviewService.Options;

namespace ReviewService.Messaging.ReviewProcessing;

public sealed class RabbitMqReviewQueuePublisher(
    IOptions<RabbitMqOptions> options,
    RabbitMqConnectionProvider connectionProvider,
    RabbitMqTopology topology) : IReviewQueuePublisher
{
    private readonly RabbitMqOptions _options = options.Value;

    public async Task PublishAsync(
        ReviewQueueMessage message,
        int? retryDelaySeconds,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return;

        var connection = await connectionProvider.GetConnectionAsync(cancellationToken);
        var channelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);
        await using var channel = await connection.CreateChannelAsync(
            channelOptions,
            cancellationToken);
        await topology.DeclareWorkAsync(channel, cancellationToken);

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, JsonHelper.Options));
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
            : _options.WorkExchangeName;
        var routingKey = retryDelaySeconds.HasValue
            ? topology.GetRetryRoutingKey(retryDelaySeconds.Value)
            : _options.WorkRoutingKey;

        await channel.BasicPublishAsync(
            exchange,
            routingKey,
            mandatory: true,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }
}

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ReviewService.Options;
using ReviewService.Contracts.Events;
using ReviewService.Contracts.Messaging;
using ReviewService.Helpers;
using ReviewService.Interfaces;
using ReviewService.Messaging;

namespace ReviewService.Messaging.IntegrationEvents;

public sealed class ReviewIntegrationConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    RabbitMqConnectionProvider connectionProvider,
    RabbitMqTopology topology,
    ILogger<ReviewIntegrationConsumer> logger) : BackgroundService
{
    private const string RetryCountHeader = "x-review-integration-retry-count";
    private readonly RabbitMqOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Review integration consumer is disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var connection = await connectionProvider.GetConnectionAsync(stoppingToken);
                var channelOptions = new CreateChannelOptions(
                    publisherConfirmationsEnabled: true,
                    publisherConfirmationTrackingEnabled: true);
                await using var channel = await connection.CreateChannelAsync(
                    channelOptions,
                    stoppingToken);
                await topology.DeclareIntegrationAsync(channel, stoppingToken);
                await channel.BasicQosAsync(
                    prefetchSize: 0,
                    prefetchCount: Math.Max((ushort)1, _options.PrefetchCount),
                    global: false,
                    cancellationToken: stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(channel);
                var shutdown = new TaskCompletionSource<ShutdownEventArgs>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                consumer.ShutdownAsync += (_, args) =>
                {
                    shutdown.TrySetResult(args);
                    return Task.CompletedTask;
                };
                consumer.ReceivedAsync += (_, args) => HandleMessageAsync(channel, args, stoppingToken);
                await channel.BasicConsumeAsync(
                    queue: _options.IntegrationQueueName,
                    autoAck: false,
                    consumerTag: string.Empty,
                    noLocal: false,
                    exclusive: false,
                    arguments: null,
                    consumer: consumer,
                    cancellationToken: stoppingToken);

                logger.LogInformation(
                    "Review integration consumer is consuming queue {QueueName}.",
                    _options.IntegrationQueueName);
                var reason = await shutdown.Task.WaitAsync(stoppingToken);
                logger.LogWarning(
                    "Review integration consumer stopped ({ReplyCode}: {ReplyText}); reconnecting.",
                    reason.ReplyCode,
                    reason.ReplyText);
                await Task.Delay(
                    TimeSpan.FromSeconds(Math.Max(1, _options.ConnectionRetryDelaySeconds)),
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Review integration consumer failed; reconnecting.");
                await Task.Delay(
                    TimeSpan.FromSeconds(Math.Max(1, _options.ConnectionRetryDelaySeconds)),
                    stoppingToken);
            }
        }
    }

    private async Task HandleMessageAsync(
        IChannel channel,
        BasicDeliverEventArgs args,
        CancellationToken cancellationToken)
    {
        try
        {
            var json = Encoding.UTF8.GetString(args.Body.ToArray());
            using var scope = scopeFactory.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<IReviewIntegrationEventHandler>();

            switch (args.RoutingKey)
            {
                case ReviewIntegrationRoutes.TestReviewPolicyPublishedV1:
                    var policy = JsonSerializer.Deserialize<TestReviewPolicyPublishedV1>(
                        json,
                        JsonHelper.Options);
                    if (policy is null)
                        throw new JsonException("Test review policy event was empty.");
                    await handler.HandleAsync(policy, cancellationToken);
                    break;

                case ReviewIntegrationRoutes.AttemptSubmittedV1:
                    var attempt = JsonSerializer.Deserialize<AttemptSubmittedV1>(
                        json,
                        JsonHelper.Options);
                    if (attempt is null)
                        throw new JsonException("Attempt submitted event was empty.");
                    await handler.HandleAsync(attempt, cancellationToken);
                    break;

                default:
                    throw new InvalidDataException(
                        $"Unsupported review integration routing key '{args.RoutingKey}'.");
            }

            await channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            logger.LogError(
                exception,
                "Review integration message {MessageId} on {RoutingKey} was dead-lettered.",
                args.BasicProperties.MessageId,
                args.RoutingKey);
            await channel.BasicRejectAsync(args.DeliveryTag, requeue: false, cancellationToken);
        }
        catch (Exception exception)
        {
            var retryCount = GetRetryCount(args.BasicProperties.Headers);
            if (retryCount >= _options.IntegrationMaxRetryAttempts)
            {
                logger.LogError(
                    exception,
                    "Integration message {MessageId} on {RoutingKey} exhausted {RetryCount} retries and was dead-lettered.",
                    args.BasicProperties.MessageId,
                    args.RoutingKey,
                    retryCount);
                await channel.BasicRejectAsync(
                    args.DeliveryTag,
                    requeue: false,
                    cancellationToken);
                return;
            }

            logger.LogWarning(
                exception,
                "Transient failure while handling integration message {MessageId} on {RoutingKey}; scheduling retry {RetryCount}.",
                args.BasicProperties.MessageId,
                args.RoutingKey,
                retryCount + 1);

            try
            {
                var retryProperties = new BasicProperties(args.BasicProperties)
                {
                    Persistent = true,
                    Headers = args.BasicProperties.Headers is null
                        ? new Dictionary<string, object?>()
                        : new Dictionary<string, object?>(args.BasicProperties.Headers)
                };
                retryProperties.Headers[RetryCountHeader] = retryCount + 1;
                await channel.BasicPublishAsync(
                    _options.IntegrationRetryExchangeName,
                    args.RoutingKey,
                    mandatory: true,
                    basicProperties: retryProperties,
                    body: args.Body,
                    cancellationToken: cancellationToken);
                await channel.BasicAckAsync(
                    args.DeliveryTag,
                    multiple: false,
                    cancellationToken);
            }
            catch (Exception retryException) when (retryException is not OperationCanceledException)
            {
                logger.LogWarning(
                    retryException,
                    "Could not schedule integration retry for message {MessageId}; requeueing the original delivery.",
                    args.BasicProperties.MessageId);
                await channel.BasicNackAsync(
                    args.DeliveryTag,
                    multiple: false,
                    requeue: true,
                    cancellationToken);
            }
        }
    }

    private static int GetRetryCount(IDictionary<string, object?>? headers)
    {
        if (headers is null
            || !headers.TryGetValue(RetryCountHeader, out var value)
            || value is null)
        {
            return 0;
        }

        return value switch
        {
            byte number => number,
            short number => number,
            int number => number,
            long number when number is >= 0 and <= int.MaxValue => (int)number,
            _ => 0
        };
    }
}

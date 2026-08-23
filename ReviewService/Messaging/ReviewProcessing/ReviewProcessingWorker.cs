using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ReviewService.Options;
using ReviewService.Enums;
using ReviewService.Helpers;
using ReviewService.Interfaces;
using ReviewService.Messaging;
using ReviewService.Models.Reviews;

namespace ReviewService.Messaging.ReviewProcessing;

public sealed class ReviewProcessingWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> rabbitMqOptions,
    IOptions<ReviewProcessingOptions> processingOptions,
    RabbitMqConnectionProvider connectionProvider,
    RabbitMqTopology topology,
    IReviewQueuePublisher publisher,
    ILogger<ReviewProcessingWorker> logger) : BackgroundService
{
    private readonly RabbitMqOptions _rabbitMqOptions = rabbitMqOptions.Value;
    private readonly ReviewProcessingOptions _processingOptions = processingOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_rabbitMqOptions.Enabled || !_processingOptions.WorkerEnabled)
        {
            logger.LogInformation("Review processing worker is disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var connection = await connectionProvider.GetConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(
                    cancellationToken: stoppingToken);
                await topology.DeclareWorkAsync(channel, stoppingToken);
                await channel.BasicQosAsync(
                    prefetchSize: 0,
                    prefetchCount: Math.Max((ushort)1, _rabbitMqOptions.PrefetchCount),
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
                    queue: _rabbitMqOptions.WorkQueueName,
                    autoAck: false,
                    consumerTag: string.Empty,
                    noLocal: false,
                    exclusive: false,
                    arguments: null,
                    consumer: consumer,
                    cancellationToken: stoppingToken);

                logger.LogInformation(
                    "Review processing worker is consuming queue {QueueName}.",
                    _rabbitMqOptions.WorkQueueName);
                var reason = await shutdown.Task.WaitAsync(stoppingToken);
                logger.LogWarning(
                    "Review processing consumer stopped ({ReplyCode}: {ReplyText}); reconnecting.",
                    reason.ReplyCode,
                    reason.ReplyText);
                await Task.Delay(
                    TimeSpan.FromSeconds(Math.Max(1, _rabbitMqOptions.ConnectionRetryDelaySeconds)),
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Review processing worker failed; reconnecting.");
                await Task.Delay(
                    TimeSpan.FromSeconds(Math.Max(1, _rabbitMqOptions.ConnectionRetryDelaySeconds)),
                    stoppingToken);
            }
        }
    }

    private async Task HandleMessageAsync(
        IChannel channel,
        BasicDeliverEventArgs args,
        CancellationToken cancellationToken)
    {
        ReviewQueueMessage? message = null;
        try
        {
            message = JsonSerializer.Deserialize<ReviewQueueMessage>(
                Encoding.UTF8.GetString(args.Body.ToArray()),
                JsonHelper.Options);
            if (message is null || message.ReviewId <= 0 || message.AttemptId == Guid.Empty)
            {
                await channel.BasicRejectAsync(args.DeliveryTag, requeue: false, cancellationToken);
                return;
            }

            using var scope = scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<IReviewJobProcessor>();
            var outcome = await processor.ProcessAsync(message, cancellationToken);
            if (outcome.Type == ReviewProcessingOutcomeType.RetryScheduled
                && outcome.RetryDelaySeconds.HasValue)
            {
                try
                {
                    await publisher.PublishAsync(
                        message,
                        outcome.RetryDelaySeconds.Value,
                        cancellationToken);
                    await MarkEnqueuedAsync(
                        scope.ServiceProvider,
                        message.ReviewId,
                        DateTimeOffset.UtcNow,
                        cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogWarning(
                        exception,
                        "Could not publish retry for review {ReviewId}; maintenance will retry.",
                        message.ReviewId);
                }
            }

            if (outcome.Type == ReviewProcessingOutcomeType.Poison)
            {
                await channel.BasicRejectAsync(args.DeliveryTag, requeue: false, cancellationToken);
                return;
            }

            await channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Malformed review work message was dead-lettered.");
            await channel.BasicRejectAsync(args.DeliveryTag, requeue: false, cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            logger.LogInformation(
                exception,
                "Ignoring stale review worker result for review {ReviewId}; a newer processing generation owns it.",
                message?.ReviewId);
            await channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Review work message failed. ReviewId: {ReviewId}. Database maintenance will retry it.",
                message?.ReviewId);
            await channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken);
        }
    }

    private static async Task MarkEnqueuedAsync(
        IServiceProvider serviceProvider,
        int reviewId,
        DateTimeOffset enqueuedAt,
        CancellationToken cancellationToken)
    {
        var dbContext = serviceProvider.GetRequiredService<ReviewService.Data.ReviewDbContext>();
        if (dbContext.Database.IsRelational())
        {
            await dbContext.Reviews
                .Where(review => review.Id == reviewId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        review => review.LastEnqueuedAt,
                        enqueuedAt),
                    cancellationToken);
            return;
        }

        var review = await dbContext.Reviews.FindAsync([reviewId], cancellationToken);
        if (review is null)
            return;

        review.LastEnqueuedAt = enqueuedAt;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

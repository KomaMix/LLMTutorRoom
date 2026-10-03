using System.Diagnostics;
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
using Shared.Observability;

namespace ReviewService.Messaging.ReviewProcessing;

public class ReviewProcessingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqOptions _rabbitMqOptions;
    private readonly ReviewProcessingOptions _processingOptions;
    private readonly RabbitMqConnectionProvider _connectionProvider;
    private readonly RabbitMqTopology _topology;
    private readonly IReviewQueuePublisher _publisher;
    private readonly ProjectMetrics _metrics;
    private readonly ILogger<ReviewProcessingWorker> _logger;

    public ReviewProcessingWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMqOptions> rabbitMqOptions,
        IOptions<ReviewProcessingOptions> processingOptions,
        RabbitMqConnectionProvider connectionProvider,
        RabbitMqTopology topology,
        IReviewQueuePublisher publisher,
        ProjectMetrics metrics,
        ILogger<ReviewProcessingWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _rabbitMqOptions = rabbitMqOptions.Value;
        _processingOptions = processingOptions.Value;
        _connectionProvider = connectionProvider;
        _topology = topology;
        _publisher = publisher;
        _metrics = metrics;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_rabbitMqOptions.Enabled || !_processingOptions.WorkerEnabled)
        {
            _logger.LogInformation("Review processing worker is disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var connection = await _connectionProvider.GetConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(
                    cancellationToken: stoppingToken);
                await _topology.DeclareWorkAsync(channel, stoppingToken);
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

                _logger.LogInformation(
                    "Review processing worker is consuming queue {QueueName}.",
                    _rabbitMqOptions.WorkQueueName);
                var reason = await shutdown.Task.WaitAsync(stoppingToken);
                _logger.LogWarning(
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
                _logger.LogWarning(exception, "Review processing worker failed; reconnecting.");
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
        var startedAt = Stopwatch.GetTimestamp();
        var metricOutcome = "failed";
        RecordDeliveryDelay(args);
        try
        {
            message = JsonSerializer.Deserialize<ReviewQueueMessage>(
                Encoding.UTF8.GetString(args.Body.ToArray()),
                JsonHelper.Options);
            if (message is null || message.ReviewId <= 0 || message.AttemptId == Guid.Empty)
            {
                metricOutcome = "poison";
                Activity.Current?.SetTag("review.processing.outcome", metricOutcome);
                await channel.BasicRejectAsync(args.DeliveryTag, requeue: false, cancellationToken);
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<IReviewJobProcessor>();
            var outcome = await processor.ProcessAsync(message, cancellationToken);
            metricOutcome = GetMetricOutcome(outcome.Type);
            Activity.Current?.SetTag("review.processing.outcome", metricOutcome);
            if (outcome.Type == ReviewProcessingOutcomeType.RetryScheduled
                && outcome.RetryDelaySeconds.HasValue)
            {
                try
                {
                    await _publisher.PublishAsync(
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
                    _logger.LogWarning(
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
            metricOutcome = "cancelled";
            throw;
        }
        catch (JsonException exception)
        {
            metricOutcome = "poison";
            Activity.Current?.SetTag("review.processing.outcome", metricOutcome);
            _logger.LogWarning(exception, "Malformed review work message was dead-lettered.");
            await channel.BasicRejectAsync(args.DeliveryTag, requeue: false, cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            metricOutcome = "stale";
            Activity.Current?.SetTag("review.processing.outcome", metricOutcome);
            _logger.LogInformation(
                exception,
                "Ignoring stale review worker result for review {ReviewId}; a newer processing generation owns it.",
                message?.ReviewId);
            await channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken);
        }
        catch (Exception exception)
        {
            metricOutcome = "failed";
            Activity.Current?.SetStatus(ActivityStatusCode.Error, exception.Message);
            Activity.Current?.SetTag("review.processing.outcome", metricOutcome);
            _logger.LogWarning(
                exception,
                "Review work message failed. ReviewId: {ReviewId}. Database maintenance will retry it.",
                message?.ReviewId);
            await channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken);
        }
        finally
        {
            _metrics.RecordReviewJob(
                metricOutcome,
                Stopwatch.GetElapsedTime(startedAt));
        }
    }

    private void RecordDeliveryDelay(BasicDeliverEventArgs args)
    {
        var unixTime = args.BasicProperties.Timestamp.UnixTime;
        if (unixTime <= 0)
        {
            return;
        }

        _metrics.RecordMessageDelivery(
            _rabbitMqOptions.WorkQueueName,
            DateTimeOffset.FromUnixTimeSeconds(unixTime));
    }

    private static string GetMetricOutcome(ReviewProcessingOutcomeType outcome)
    {
        return outcome switch
        {
            ReviewProcessingOutcomeType.Ignored => "ignored",
            ReviewProcessingOutcomeType.Completed => "completed",
            ReviewProcessingOutcomeType.RetryScheduled => "retry-scheduled",
            ReviewProcessingOutcomeType.Poison => "poison",
            _ => "unknown"
        };
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
        {
            return;
        }

        review.LastEnqueuedAt = enqueuedAt;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

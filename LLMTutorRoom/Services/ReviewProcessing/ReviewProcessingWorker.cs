using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace LLMTutorRoom.Services.ReviewProcessing
{
    public sealed class ReviewProcessingWorker : BackgroundService
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly RabbitMqOptions _rabbitMqOptions;
        private readonly ReviewProcessingOptions _processingOptions;
        private readonly RabbitMqConnectionProvider _connectionProvider;
        private readonly RabbitMqReviewTopology _topology;
        private readonly IReviewQueuePublisher _publisher;
        private readonly ILogger<ReviewProcessingWorker> _logger;

        public ReviewProcessingWorker(
            IServiceScopeFactory scopeFactory,
            IOptions<RabbitMqOptions> rabbitMqOptions,
            IOptions<ReviewProcessingOptions> processingOptions,
            RabbitMqConnectionProvider connectionProvider,
            RabbitMqReviewTopology topology,
            IReviewQueuePublisher publisher,
            ILogger<ReviewProcessingWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _rabbitMqOptions = rabbitMqOptions.Value;
            _processingOptions = processingOptions.Value;
            _connectionProvider = connectionProvider;
            _topology = topology;
            _publisher = publisher;
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

                    await _topology.DeclareAsync(channel, stoppingToken);
                    await channel.BasicQosAsync(
                        prefetchSize: 0,
                        prefetchCount: Math.Max((ushort)1, _rabbitMqOptions.PrefetchCount),
                        global: false,
                        cancellationToken: stoppingToken);

                    var consumer = new AsyncEventingBasicConsumer(channel);
                    consumer.ReceivedAsync += async (_, args) =>
                    {
                        await HandleMessageAsync(channel, args, stoppingToken);
                    };

                    await channel.BasicConsumeAsync(
                        queue: _rabbitMqOptions.QueueName,
                        autoAck: false,
                        consumerTag: string.Empty,
                        noLocal: false,
                        exclusive: false,
                        arguments: null,
                        consumer: consumer,
                        cancellationToken: stoppingToken);

                    _logger.LogInformation(
                        "Review processing worker is consuming queue {QueueName}.",
                        _rabbitMqOptions.QueueName);

                    await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Review processing worker failed. It will retry the RabbitMQ connection.");

                    await Task.Delay(
                        TimeSpan.FromSeconds(_rabbitMqOptions.ConnectionRetryDelaySeconds),
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
                var body = Encoding.UTF8.GetString(args.Body.ToArray());
                message = JsonSerializer.Deserialize<ReviewQueueMessage>(body, JsonOptions);

                if (message is null || message.ReviewId <= 0 || message.AttemptId <= 0)
                {
                    await channel.BasicRejectAsync(
                        args.DeliveryTag,
                        requeue: false,
                        cancellationToken);
                    return;
                }

                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<ReviewJobProcessor>();
                var outcome = await processor.ProcessAsync(message, cancellationToken);

                if (outcome.Type == ReviewProcessingOutcomeType.RetryScheduled
                    && outcome.RetryDelaySeconds.HasValue)
                {
                    try
                    {
                        await _publisher.PublishAsync(
                            message,
                            outcome.RetryDelaySeconds.Value,
                            cancellationToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogWarning(
                            ex,
                            "Could not publish retry for review {ReviewId}. Maintenance worker will retry from PostgreSQL.",
                            message.ReviewId);
                    }
                }

                if (outcome.Type == ReviewProcessingOutcomeType.Poison)
                {
                    await channel.BasicRejectAsync(
                        args.DeliveryTag,
                        requeue: false,
                        cancellationToken);
                    return;
                }

                await channel.BasicAckAsync(
                    args.DeliveryTag,
                    multiple: false,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Review queue message failed before it could be handled. ReviewId: {ReviewId}.",
                    message?.ReviewId);

                await channel.BasicNackAsync(
                    args.DeliveryTag,
                    multiple: false,
                    requeue: false,
                    cancellationToken);
            }
        }
    }
}

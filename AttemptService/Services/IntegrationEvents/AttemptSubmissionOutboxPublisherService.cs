using System.Diagnostics;
using System.Text;
using AttemptService.Data;
using AttemptService.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using ReviewService.Contracts.Events;
using ReviewService.Contracts.Messaging;
using Shared.Observability;

namespace AttemptService.Services.IntegrationEvents;

public class AttemptSubmissionOutboxPublisherService : BackgroundService
{
    private const string OutboxName = "attempt-submission";
    private readonly IDbContextFactory<AttemptDbContext> _dbContextFactory;
    private readonly RabbitMqConnectionProvider _connectionProvider;
    private readonly RabbitMqOptions _rabbitMqOptions;
    private readonly AttemptOutboxOptions _outboxOptions;
    private readonly ProjectMetrics _metrics;
    private readonly ILogger<AttemptSubmissionOutboxPublisherService> _logger;

    public AttemptSubmissionOutboxPublisherService(
        IDbContextFactory<AttemptDbContext> dbContextFactory,
        RabbitMqConnectionProvider connectionProvider,
        IOptions<RabbitMqOptions> rabbitMqOptions,
        IOptions<AttemptOutboxOptions> outboxOptions,
        ProjectMetrics metrics,
        ILogger<AttemptSubmissionOutboxPublisherService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _connectionProvider = connectionProvider;
        _rabbitMqOptions = rabbitMqOptions.Value;
        _outboxOptions = outboxOptions.Value;
        _metrics = metrics;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_outboxOptions.Enabled || !_rabbitMqOptions.Enabled)
        {
            _logger.LogInformation("Attempt submission outbox publisher is disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Attempt submission outbox pass failed.");
            }

            await Task.Delay(
                TimeSpan.FromSeconds(_outboxOptions.PollIntervalSeconds),
                stoppingToken);
        }
    }

    public async Task<int> PublishPendingAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var messages = await dbContext.AttemptSubmissionOutboxMessages
            .Where(message => !message.PublishedAt.HasValue
                && (!message.NextPublishAt.HasValue || message.NextPublishAt <= now))
            .OrderBy(message => message.OccurredAt)
            .Take(_outboxOptions.BatchSize)
            .ToListAsync(cancellationToken);
        if (messages.Count == 0)
        {
            return 0;
        }

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

        var publishedCount = 0;
        foreach (var message in messages)
        {
            using var activity = ProjectTelemetry.StartOutboxPublishActivity(
                OutboxName,
                message.Id,
                message.TraceParent,
                message.TraceState);
            try
            {
                var properties = new BasicProperties
                {
                    Persistent = true,
                    ContentType = "application/json",
                    MessageId = message.Id.ToString(),
                    Type = nameof(AttemptSubmittedV1),
                    Timestamp = new AmqpTimestamp(message.OccurredAt.ToUnixTimeSeconds())
                };
                await channel.BasicPublishAsync(
                    ReviewIntegrationRoutes.ExchangeName,
                    ReviewIntegrationRoutes.AttemptSubmittedV1,
                    mandatory: true,
                    basicProperties: properties,
                    body: Encoding.UTF8.GetBytes(message.PayloadJson),
                    cancellationToken: cancellationToken);

                message.PublishAttempts++;
                message.PublishedAt = DateTimeOffset.UtcNow;
                message.NextPublishAt = null;
                message.LastError = string.Empty;
                publishedCount++;
                activity?.SetStatus(ActivityStatusCode.Ok);
                _metrics.RecordOutboxPublish(OutboxName, "published", message.OccurredAt);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                message.PublishAttempts++;
                message.NextPublishAt = DateTimeOffset.UtcNow.AddSeconds(
                    _outboxOptions.RetryDelaySeconds);
                message.LastError = Truncate(exception.Message, 2000);
                activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
                activity?.SetTag("error.type", exception.GetType().FullName);
                _metrics.RecordOutboxPublish(OutboxName, "failed", message.OccurredAt);
                _logger.LogWarning(
                    exception,
                    "Could not publish attempt submission event {EventId} for attempt {AttemptId}.",
                    message.Id,
                    message.AttemptId);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return publishedCount;
    }

    private static string Truncate(string value, int maximumLength)
    {
        return value.Length <= maximumLength ? value : value[..maximumLength];
    }
}

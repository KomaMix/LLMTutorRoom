using System.Text;
using AttemptService.Data;
using AttemptService.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using ReviewService.Contracts.Events;
using ReviewService.Contracts.Messaging;

namespace AttemptService.Services.IntegrationEvents;

public sealed class AttemptSubmissionOutboxPublisherService(
    IDbContextFactory<AttemptDbContext> dbContextFactory,
    RabbitMqConnectionProvider connectionProvider,
    IOptions<RabbitMqOptions> rabbitMqOptions,
    IOptions<AttemptOutboxOptions> outboxOptions,
    ILogger<AttemptSubmissionOutboxPublisherService> logger) : BackgroundService
{
    private readonly RabbitMqOptions _rabbitMqOptions = rabbitMqOptions.Value;
    private readonly AttemptOutboxOptions _outboxOptions = outboxOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_outboxOptions.Enabled || !_rabbitMqOptions.Enabled)
        {
            logger.LogInformation("Attempt submission outbox publisher is disabled.");
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
                logger.LogWarning(exception, "Attempt submission outbox pass failed.");
            }

            await Task.Delay(
                TimeSpan.FromSeconds(_outboxOptions.PollIntervalSeconds),
                stoppingToken);
        }
    }

    public async Task<int> PublishPendingAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var messages = await dbContext.AttemptSubmissionOutboxMessages
            .Where(message => !message.PublishedAt.HasValue
                && (!message.NextPublishAt.HasValue || message.NextPublishAt <= now))
            .OrderBy(message => message.OccurredAt)
            .Take(_outboxOptions.BatchSize)
            .ToListAsync(cancellationToken);
        if (messages.Count == 0)
            return 0;

        var connection = await connectionProvider.GetConnectionAsync(cancellationToken);
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
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                message.PublishAttempts++;
                message.NextPublishAt = DateTimeOffset.UtcNow.AddSeconds(
                    _outboxOptions.RetryDelaySeconds);
                message.LastError = Truncate(exception.Message, 2000);
                logger.LogWarning(
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

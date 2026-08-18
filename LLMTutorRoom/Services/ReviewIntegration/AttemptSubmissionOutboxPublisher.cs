using System.Text;
using LLMTutorRoom.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using ReviewService.Contracts.Messaging;

namespace LLMTutorRoom.Services.ReviewIntegration;

public sealed class AttemptSubmissionOutboxPublisher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqConnectionProvider _connectionProvider;
    private readonly RabbitMqOptions _rabbitMqOptions;
    private readonly AttemptSubmissionOutboxOptions _options;
    private readonly ILogger<AttemptSubmissionOutboxPublisher> _logger;

    public AttemptSubmissionOutboxPublisher(
        IServiceScopeFactory scopeFactory,
        RabbitMqConnectionProvider connectionProvider,
        IOptions<RabbitMqOptions> rabbitMqOptions,
        IOptions<AttemptSubmissionOutboxOptions> options,
        ILogger<AttemptSubmissionOutboxPublisher> logger)
    {
        _scopeFactory = scopeFactory;
        _connectionProvider = connectionProvider;
        _rabbitMqOptions = rabbitMqOptions.Value;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled || !_rabbitMqOptions.Enabled)
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
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Attempt submission outbox pass failed.");
            }

            await Task.Delay(
                TimeSpan.FromSeconds(Math.Max(1, _options.PollIntervalSeconds)),
                stoppingToken);
        }
    }

    private async Task PublishPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TutorRoomDbContext>();
        var now = DateTimeOffset.UtcNow;
        var messages = await dbContext.AttemptSubmissionOutboxMessages
            .Where(message => !message.PublishedAt.HasValue
                && (!message.NextPublishAt.HasValue || message.NextPublishAt <= now))
            .OrderBy(message => message.OccurredAt)
            .Take(Math.Max(1, _options.BatchSize))
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
            return;

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

        foreach (var message in messages)
        {
            try
            {
                var properties = new BasicProperties
                {
                    Persistent = true,
                    ContentType = "application/json",
                    MessageId = message.Id.ToString(),
                    Type = nameof(ReviewService.Contracts.Events.AttemptSubmittedV1),
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
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.PublishAttempts++;
                message.NextPublishAt = DateTimeOffset.UtcNow.AddSeconds(
                    Math.Max(1, _options.RetryDelaySeconds));
                message.LastError = ex.Message;
                _logger.LogWarning(
                    ex,
                    "Could not publish attempt submission event {EventId} for attempt {AttemptId}.",
                    message.Id,
                    message.AttemptId);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

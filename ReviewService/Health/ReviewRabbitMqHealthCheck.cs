using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using ReviewService.Options;
using ReviewService.Messaging;

namespace ReviewService.Health;

public sealed class ReviewRabbitMqHealthCheck(
    IOptions<RabbitMqOptions> options,
    RabbitMqConnectionProvider connectionProvider) : IHealthCheck
{
    private readonly RabbitMqOptions _options = options.Value;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return HealthCheckResult.Healthy("RabbitMQ integration is disabled.");

        try
        {
            var connection = await connectionProvider.GetConnectionAsync(cancellationToken);
            return connection.IsOpen
                ? HealthCheckResult.Healthy("RabbitMQ connection is open.")
                : HealthCheckResult.Unhealthy("RabbitMQ connection is closed.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy(
                "RabbitMQ is unavailable.",
                exception);
        }
    }
}

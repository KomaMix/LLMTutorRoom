using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using ReviewService.Data;
using ReviewService.ExceptionHandling;
using ReviewService.Health;
using ReviewService.Interfaces;
using ReviewService.Messaging;
using ReviewService.Messaging.IntegrationEvents;
using ReviewService.Messaging.ReviewProcessing;
using ReviewService.Options;
using ReviewService.Services;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");
const int maxRabbitMqTtlSeconds = int.MaxValue / 1000;

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower));
    });
builder.Services.AddOpenApi();
builder.Services.AddExceptionHandler<ReviewApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddDbContext<ReviewDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddOptions<RabbitMqOptions>()
    .Bind(builder.Configuration.GetSection(RabbitMqOptions.SectionName))
    .Validate(options => !options.Enabled
        || (!string.IsNullOrWhiteSpace(options.HostName)
            && options.Port > 0
            && !string.IsNullOrWhiteSpace(options.IntegrationQueueName)
            && !string.IsNullOrWhiteSpace(options.IntegrationRetryExchangeName)
            && !string.IsNullOrWhiteSpace(options.IntegrationRetryQueueName)
            && options.IntegrationRetryDelaySeconds is > 0 and <= maxRabbitMqTtlSeconds
            && options.IntegrationMaxRetryAttempts > 0
            && !string.IsNullOrWhiteSpace(options.IntegrationDeadLetterExchangeName)
            && !string.IsNullOrWhiteSpace(options.IntegrationDeadLetterQueueName)
            && !string.IsNullOrWhiteSpace(options.IntegrationDeadLetterRoutingKey)
            && !string.IsNullOrWhiteSpace(options.WorkExchangeName)
            && !string.IsNullOrWhiteSpace(options.WorkQueueName)
            && !string.IsNullOrWhiteSpace(options.WorkRoutingKey)
            && !string.IsNullOrWhiteSpace(options.RetryExchangeName)
            && !string.IsNullOrWhiteSpace(options.RetryQueueNamePrefix)
            && !string.IsNullOrWhiteSpace(options.RetryRoutingKeyPrefix)
            && !string.IsNullOrWhiteSpace(options.DeadLetterExchangeName)
            && !string.IsNullOrWhiteSpace(options.DeadLetterQueueName)
            && !string.IsNullOrWhiteSpace(options.DeadLetterRoutingKey)
            && options.RetryDelaysSeconds is { Length: > 0 }
            && options.RetryDelaysSeconds.All(delay => delay is > 0 and <= maxRabbitMqTtlSeconds)
            && options.RetryDelaysSeconds.Distinct().Count() == options.RetryDelaysSeconds.Length
            && options.ConnectionRetryDelaySeconds > 0),
        "RabbitMq contains invalid connection or topology settings.")
    .ValidateOnStart();
builder.Services.AddOptions<ReviewProcessingOptions>()
    .Bind(builder.Configuration.GetSection(ReviewProcessingOptions.SectionName))
    .Validate(options => Uri.TryCreate(options.LlmGatewayBaseUrl, UriKind.Absolute, out _),
        "ReviewProcessing:LlmGatewayBaseUrl must be an absolute URL.")
    .Validate(options => options.LlmRequestTimeoutSeconds > 0
            && options.ProcessingLeaseSeconds > options.LlmRequestTimeoutSeconds
            && options.QueueMaintenanceIntervalSeconds > 0
            && options.EnqueueThrottleSeconds > 0,
        "ReviewProcessing contains invalid timeout or retry settings; the processing lease must exceed one LLM request timeout.")
    .ValidateOnStart();
builder.Services.AddOptions<ReviewStorageOptions>()
    .Bind(builder.Configuration.GetSection(ReviewStorageOptions.SectionName))
    .Validate(options => options.InboxRetentionDays > 0
            && options.CleanupIntervalMinutes > 0
            && options.CleanupBatchSize > 0
            && options.PendingSubmissionWarningHours > 0,
        "ReviewStorage contains invalid retention or cleanup settings.")
    .ValidateOnStart();

builder.Services.AddSingleton<IReviewScoringService, ReviewScoringService>();
builder.Services.AddScoped<ITeacherModelAccessService, TeacherModelAccessService>();
builder.Services.AddScoped<IReviewCreationService, ReviewCreationService>();
builder.Services.AddScoped<IReviewIntegrationEventHandler, ReviewIntegrationEventHandler>();
builder.Services.AddScoped<IReviewQueryService, ReviewQueryService>();
builder.Services.AddScoped<IReviewJobProcessor, ReviewJobProcessor>();
builder.Services.AddSingleton<RabbitMqConnectionProvider>();
builder.Services.AddSingleton<RabbitMqTopology>();
builder.Services.AddSingleton<IReviewQueuePublisher, RabbitMqReviewQueuePublisher>();

builder.Services.AddHttpClient<ILlmGatewayReviewClient, LlmGatewayReviewClient>((services, client) =>
{
    var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<ReviewProcessingOptions>>().Value;
    client.BaseAddress = new Uri(options.LlmGatewayBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.LlmRequestTimeoutSeconds);
});
builder.Services.AddHttpClient<ILlmGatewayModelCatalogClient, LlmGatewayModelCatalogClient>((services, client) =>
{
    var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<ReviewProcessingOptions>>().Value;
    client.BaseAddress = new Uri(options.LlmGatewayBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.LlmRequestTimeoutSeconds);
});
builder.Services.AddHostedService<ReviewIntegrationConsumer>();
builder.Services.AddHostedService<ReviewProcessingWorker>();
builder.Services.AddHostedService<ReviewQueueMaintenanceService>();
builder.Services.AddHostedService<ReviewStorageCleanupService>();
builder.Services.AddHealthChecks()
    .AddCheck<ReviewDatabaseHealthCheck>("review-database", tags: ["ready"])
    .AddCheck<ReviewRabbitMqHealthCheck>("review-rabbitmq", tags: ["ready"]);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ReviewDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
});
app.MapControllers();

await app.RunAsync();

public partial class Program;

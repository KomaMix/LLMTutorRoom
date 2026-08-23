using System.Text.Json;
using System.Text.Json.Serialization;
using AttemptService.Data;
using AttemptService.Health;
using AttemptService.Interfaces;
using AttemptService.Options;
using AttemptService.Services;
using AttemptService.Services.IntegrationEvents;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Shared.Auth;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");
var inputLimits = builder.Configuration
    .GetSection(AttemptInputLimitsOptions.SectionName)
    .Get<AttemptInputLimitsOptions>()
    ?? new AttemptInputLimitsOptions();

builder.WebHost.ConfigureKestrel(options =>
    options.Limits.MaxRequestBodySize = inputLimits.MaxRequestBodyBytes);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower));
    });
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddPooledDbContextFactory<AttemptDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddOptions<TeachingServiceOptions>()
    .Bind(builder.Configuration.GetSection(TeachingServiceOptions.SectionName))
    .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _),
        "TeachingService:BaseUrl must be an absolute URL.")
    .Validate(options => options.RequestTimeoutSeconds > 0,
        "TeachingService request timeout must be positive.")
    .ValidateOnStart();
builder.Services.AddOptions<RabbitMqOptions>()
    .Bind(builder.Configuration.GetSection(RabbitMqOptions.SectionName))
    .Validate(options => !options.Enabled
        || (!string.IsNullOrWhiteSpace(options.HostName)
            && options.Port > 0
            && !string.IsNullOrWhiteSpace(options.VirtualHost)
            && !string.IsNullOrWhiteSpace(options.UserName)),
        "RabbitMq contains invalid connection settings.")
    .ValidateOnStart();
builder.Services.AddOptions<AttemptOutboxOptions>()
    .Bind(builder.Configuration.GetSection(AttemptOutboxOptions.SectionName))
    .Validate(options => options.PollIntervalSeconds > 0
            && options.RetryDelaySeconds > 0
            && options.BatchSize > 0
            && options.PublishedMessageRetentionDays > 0
            && options.CleanupIntervalMinutes > 0
            && options.CleanupBatchSize > 0,
        "AttemptOutbox contains invalid polling, retry, or retention settings.")
    .ValidateOnStart();
builder.Services.AddOptions<AttemptExpirationOptions>()
    .Bind(builder.Configuration.GetSection(AttemptExpirationOptions.SectionName))
    .Validate(options => options.PollIntervalSeconds > 0 && options.BatchSize > 0,
        "AttemptExpiration contains invalid polling settings.")
    .ValidateOnStart();
builder.Services.AddOptions<AttemptInputLimitsOptions>()
    .Bind(builder.Configuration.GetSection(AttemptInputLimitsOptions.SectionName))
    .Validate(options => options.MaxRequestBodyBytes > 0
            && options.MaxAnswerCount > 0
            && options.MaxAnswerLength > 0
            && options.MaxTotalAnswerLength >= options.MaxAnswerLength,
        "AttemptInputLimits contains invalid body or answer limits.")
    .ValidateOnStart();

builder.Services.AddScoped<IAttemptLifecycleService, AttemptLifecycleService>();
builder.Services.AddSingleton<RabbitMqConnectionProvider>();
builder.Services.AddHttpClient<ITeachingServiceClient, TeachingServiceClient>((services, client) =>
{
    var options = services
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<TeachingServiceOptions>>()
        .Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
});
builder.Services.AddHostedService<AttemptExpirationService>();
builder.Services.AddHostedService<AttemptSubmissionOutboxPublisherService>();
builder.Services.AddHostedService<AttemptOutboxCleanupService>();
builder.Services.AddHealthChecks()
    .AddCheck<AttemptDatabaseHealthCheck>("attempt-database", tags: ["ready"])
    .AddCheck<AttemptRabbitMqHealthCheck>("attempt-rabbitmq", tags: ["ready"]);
builder.Services.AddJwtAuthentication(builder.Configuration);

var app = builder.Build();

await using (var dbContext = await app.Services
    .GetRequiredService<IDbContextFactory<AttemptDbContext>>()
    .CreateDbContextAsync())
{
    await dbContext.Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.Use(HandleBadHttpRequestAsync);

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

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();

static async Task HandleBadHttpRequestAsync(HttpContext context, RequestDelegate next)
{
    try
    {
        await next(context);
    }
    catch (BadHttpRequestException exception)
    {
        context.Response.StatusCode = exception.StatusCode;
    }
}

public partial class Program;

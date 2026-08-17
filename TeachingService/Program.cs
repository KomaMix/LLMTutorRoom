using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Shared.Auth;
using TeachingService.Data;
using TeachingService.Interfaces;
using TeachingService.Options;
using TeachingService.Services.IntegrationEvents;
using TeachingService.Services;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower));
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddPooledDbContextFactory<TeachingDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddScoped(provider => provider
    .GetRequiredService<IDbContextFactory<TeachingDbContext>>()
    .CreateDbContext());
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddOptions<IntegrationEventOptions>()
    .Bind(builder.Configuration.GetSection(IntegrationEventOptions.SectionName))
    .Validate(options => !options.Enabled
        || (!string.IsNullOrWhiteSpace(options.HostName)
            && options.Port > 0
            && options.PollIntervalMilliseconds > 0
            && options.BatchSize > 0
            && options.RetryBaseDelaySeconds > 0
            && options.RetryMaxDelaySeconds >= options.RetryBaseDelaySeconds
            && options.PublishedMessageRetentionDays > 0
            && options.CleanupIntervalMinutes > 0
            && options.CleanupBatchSize > 0),
        "IntegrationEvents contains invalid RabbitMQ or retry settings.")
    .ValidateOnStart();
builder.Services.AddScoped<TeachingCatalogService>();
builder.Services.AddScoped<IReviewPolicyOutboxWriter, ReviewPolicyOutboxWriter>();
builder.Services.AddSingleton<RabbitMqIntegrationConnectionProvider>();
builder.Services.AddSingleton<IIntegrationEventPublisher, RabbitMqIntegrationEventPublisher>();
builder.Services.AddHostedService<IntegrationOutboxPublisherService>();
builder.Services.AddHostedService<IntegrationOutboxCleanupService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<TeachingDbContext>();
    await dbContext.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

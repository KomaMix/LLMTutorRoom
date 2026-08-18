using LLMTutorRoom.Data;
using LLMTutorRoom.Interfaces;
using LLMTutorRoom.Services;
using LLMTutorRoom.Services.ReviewIntegration;
using LLMTutorRoom.Services.Reviews;
using LLMTutorRoom.Services.Teaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.StaticFiles;
using Shared.Auth;
using System.Text.Json;
using System.Text.Json.Serialization;

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
builder.Services.AddDbContext<TutorRoomDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddOptions<RabbitMqOptions>()
    .Bind(builder.Configuration.GetSection(RabbitMqOptions.SectionName))
    .Validate(options => !options.Enabled
        || (!string.IsNullOrWhiteSpace(options.HostName)
            && options.Port > 0
            && !string.IsNullOrWhiteSpace(options.UserName)),
        "RabbitMq contains invalid connection settings.")
    .ValidateOnStart();
builder.Services.AddOptions<AttemptSubmissionOutboxOptions>()
    .Bind(builder.Configuration.GetSection(AttemptSubmissionOutboxOptions.SectionName))
    .Validate(options => options.PollIntervalSeconds > 0, "Outbox poll interval must be positive.")
    .Validate(options => options.RetryDelaySeconds > 0, "Outbox retry delay must be positive.")
    .Validate(options => options.BatchSize > 0, "Outbox batch size must be positive.")
    .Validate(options => options.PublishedMessageRetentionDays > 0,
        "Outbox retention must be positive.")
    .Validate(options => options.CleanupIntervalMinutes > 0,
        "Outbox cleanup interval must be positive.")
    .Validate(options => options.CleanupBatchSize > 0,
        "Outbox cleanup batch size must be positive.")
    .ValidateOnStart();
builder.Services.AddOptions<TeachingServiceOptions>()
    .Bind(builder.Configuration.GetSection(TeachingServiceOptions.SectionName))
    .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _),
        "TeachingService:BaseUrl must be an absolute URL.")
    .Validate(options => options.RequestTimeoutSeconds > 0,
        "TeachingService request timeout must be positive.")
    .ValidateOnStart();
builder.Services.AddOptions<ReviewServiceOptions>()
    .Bind(builder.Configuration.GetSection(ReviewServiceOptions.SectionName))
    .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _),
        "ReviewService:BaseUrl must be an absolute URL.")
    .Validate(options => options.RequestTimeoutSeconds > 0,
        "ReviewService request timeout must be positive.")
    .ValidateOnStart();
builder.Services.AddScoped<ClassroomService>();
builder.Services.AddSingleton<RabbitMqConnectionProvider>();
builder.Services.AddHttpClient<ITeachingServiceClient, TeachingServiceClient>((serviceProvider, client) =>
{
    var options = serviceProvider
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<TeachingServiceOptions>>()
        .Value;

    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
});
builder.Services.AddHttpClient<IReviewServiceClient, ReviewServiceClient>((serviceProvider, client) =>
{
    var options = serviceProvider
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<ReviewServiceOptions>>()
        .Value;

    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
});
builder.Services.AddHostedService<AttemptSubmissionOutboxPublisher>();
builder.Services.AddHostedService<AttemptSubmissionOutboxCleanupService>();
builder.Services.AddJwtAuthentication(builder.Configuration);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<TutorRoomDbContext>();
    await dbContext.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = DisableIndexHtmlCache
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapFallbackToFile("index.html", new StaticFileOptions
{
    OnPrepareResponse = DisableIndexHtmlCache
});

app.Run();

static void DisableIndexHtmlCache(StaticFileResponseContext context)
{
    if (!string.Equals(context.File.Name, "index.html", StringComparison.OrdinalIgnoreCase))
        return;

    context.Context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
    context.Context.Response.Headers["Pragma"] = "no-cache";
    context.Context.Response.Headers["Expires"] = "0";
}

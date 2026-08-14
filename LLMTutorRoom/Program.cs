using LLMTutorRoom.Data;
using LLMTutorRoom.Services;
using LLMTutorRoom.Services.ReviewProcessing;
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
builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection("RabbitMq"));
builder.Services.Configure<ReviewProcessingOptions>(
    builder.Configuration.GetSection("ReviewProcessing"));
builder.Services.Configure<TeachingServiceOptions>(
    builder.Configuration.GetSection(TeachingServiceOptions.SectionName));
builder.Services.AddScoped<ClassroomService>();
builder.Services.AddSingleton<ReviewScoringService>();
builder.Services.AddSingleton<RabbitMqConnectionProvider>();
builder.Services.AddSingleton<RabbitMqReviewTopology>();
builder.Services.AddSingleton<IReviewQueuePublisher, RabbitMqReviewQueuePublisher>();
builder.Services.AddScoped<ReviewJobProcessor>();
builder.Services.AddHttpClient<LlmGatewayReviewClient>((serviceProvider, client) =>
{
    var options = serviceProvider
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<ReviewProcessingOptions>>()
        .Value;

    client.BaseAddress = new Uri(options.LlmGatewayBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.LlmRequestTimeoutSeconds);
});
builder.Services.AddHttpClient<LlmGatewayModelCatalogClient>((serviceProvider, client) =>
{
    var options = serviceProvider
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<ReviewProcessingOptions>>()
        .Value;

    client.BaseAddress = new Uri(options.LlmGatewayBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.LlmRequestTimeoutSeconds);
});
builder.Services.AddScoped<TeacherModelAccessService>();
builder.Services.AddHttpClient<ITeachingServiceClient, TeachingServiceClient>((serviceProvider, client) =>
{
    var options = serviceProvider
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<TeachingServiceOptions>>()
        .Value;

    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
});
builder.Services.AddHostedService<ReviewProcessingWorker>();
builder.Services.AddHostedService<ReviewQueueMaintenanceService>();
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

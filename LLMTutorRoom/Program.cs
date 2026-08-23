using LLMTutorRoom.Interfaces;
using LLMTutorRoom.Options;
using LLMTutorRoom.Services;
using LLMTutorRoom.Services.Attempts;
using LLMTutorRoom.Services.Reviews;
using LLMTutorRoom.Services.Teaching;
using Microsoft.AspNetCore.StaticFiles;
using Shared.Auth;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower));
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddOptions<AttemptServiceOptions>()
    .Bind(builder.Configuration.GetSection(AttemptServiceOptions.SectionName))
    .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _),
        "AttemptService:BaseUrl must be an absolute URL.")
    .Validate(options => options.RequestTimeoutSeconds > 0,
        "AttemptService request timeout must be positive.")
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
builder.Services.AddHttpClient<IAttemptServiceClient, AttemptServiceClient>((serviceProvider, client) =>
{
    var options = serviceProvider
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<AttemptServiceOptions>>()
        .Value;

    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
});
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
builder.Services.AddJwtAuthentication(builder.Configuration);

var app = builder.Build();

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

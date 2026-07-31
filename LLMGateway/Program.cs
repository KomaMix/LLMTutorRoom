using LLMGateway.Data;
using LLMGateway.Interfaces;
using LLMGateway.Services;
using LLMGateway.Services.LLMCreators;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<IChatClientCreator, OllamaChatClientCreator>();
builder.Services.AddSingleton<IChatClientCreator, OpenAiCompatibleChatClientCreator>();

builder.Services.AddSingleton<ChatClientFactory>();
builder.Services.AddSingleton<RateLimitService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<ChatExecutionService>();
builder.Services.AddProblemDetails();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Unexpected error",
            detail: "An unexpected server error occurred.")
            .ExecuteAsync(context);
    });
});

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program
{
}

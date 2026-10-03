using LLMGateway.Middleware;
using Shared.Auth;
using LLMGateway.Data;
using LLMGateway.Services;
using Microsoft.EntityFrameworkCore;
using Shared.Observability;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.AddProjectObservability("llm-gateway");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<ChatClientFactory>();
builder.Services.AddSingleton<RateLimitService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<ChatExecutionService>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GatewayExceptionHandler>();
builder.Services.AddScoped<IModelManagementService, ModelManagementService>();
builder.Services.AddScoped<IDeploymentProbe, DeploymentProbe>();
builder.Services.AddJwtAuthentication(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (dbContext.Database.IsRelational())
        await dbContext.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program
{
}

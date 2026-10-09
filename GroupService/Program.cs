using Microsoft.EntityFrameworkCore;
using Shared.Auth;
using Shared.Observability;
using GroupService.Data;
using GroupService.Services;

var builder = WebApplication.CreateBuilder(args);
builder.AddProjectObservability("group-service");
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddPooledDbContextFactory<GroupDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddScoped(provider => provider
    .GetRequiredService<IDbContextFactory<GroupDbContext>>()
    .CreateDbContext());
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddScoped<StudyGroupService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<GroupDbContext>();
    await dbContext.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

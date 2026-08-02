using LLMTutorRoom.Data;
using LLMTutorRoom.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
var jwtIssuer = builder.Configuration["Auth:Jwt:Issuer"] ?? "LLMTutorRoom";
var jwtAudience = builder.Configuration["Auth:Jwt:Audience"] ?? "LLMTutorRoom.Client";
var jwtSigningKey = builder.Configuration["Auth:Jwt:SigningKey"]
    ?? throw new InvalidOperationException("Auth:Jwt:SigningKey is required.");
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower));
    });
builder.Services.AddOpenApi();
builder.Services.AddDbContext<TutorRoomDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddScoped<ClassroomService>();
builder.Services.AddScoped<AuthService>();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            NameClaimType = System.Security.Claims.ClaimTypes.Name,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<TutorRoomDbContext>();
    await dbContext.Database.MigrateAsync();

    var authService = scope.ServiceProvider.GetRequiredService<AuthService>();
    await authService.EnsureConfiguredUsersAsync(CancellationToken.None);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
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

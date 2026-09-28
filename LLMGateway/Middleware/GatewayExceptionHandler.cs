using LLMGateway.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LLMGateway.Middleware;

public sealed class GatewayExceptionHandler(ILogger<GatewayExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            ModelKeyConflictException => (409, "A model with this key already exists."),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } }
                => (409, "A record with these values already exists."),
            KeyNotFoundException => (404, "The requested resource was not found."),
            ArgumentException => (400, "The deployment API key settings are invalid."),
            _ => (500, "An unexpected server error occurred.")
        };
        logger.LogWarning("Gateway request failed with status {Status}, exception type {ExceptionType}.", status, exception.GetType().Name);
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails { Status = status, Title = title }, ct);
        return true;
    }
}

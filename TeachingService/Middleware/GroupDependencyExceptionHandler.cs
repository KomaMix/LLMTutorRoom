using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace TeachingService.Middleware;

public class GroupDependencyExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not HttpRequestException
            && (exception is not OperationCanceledException || httpContext.RequestAborted.IsCancellationRequested))
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status503ServiceUnavailable,
            Title = "Group service is unavailable.",
            Detail = "Group access could not be verified. Retry the request later."
        }, cancellationToken);
        return true;
    }
}

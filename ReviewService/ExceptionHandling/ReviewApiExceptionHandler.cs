using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ReviewService.ExceptionHandling;

public sealed class ReviewApiExceptionHandler(
    ILogger<ReviewApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var isUpstreamFailure = exception is HttpRequestException
            || exception is TaskCanceledException
                && !httpContext.RequestAborted.IsCancellationRequested;
        if (!isUpstreamFailure)
            return false;

        logger.LogWarning(
            exception,
            "An upstream service was unavailable while handling {Path}.",
            httpContext.Request.Path);
        httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Upstream service unavailable",
                Detail = "A service required to complete the request is temporarily unavailable.",
                Instance = httpContext.Request.Path
            },
            cancellationToken);
        return true;
    }
}

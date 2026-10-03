using System.Diagnostics;

namespace Shared.Observability;

public static class ProjectTelemetry
{
    public const string ActivitySourceName = "LLMTutorRoom.Application";
    public const string MeterName = "LLMTutorRoom.Application";
    public const string LlmTelemetryName = "LLMTutorRoom.LLM";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    public static (string? TraceParent, string? TraceState) CaptureCurrentTraceContext()
    {
        var activity = Activity.Current;
        if (activity?.IdFormat != ActivityIdFormat.W3C)
        {
            return (null, null);
        }

        return (activity.Id, activity.TraceStateString);
    }

    public static Activity? StartOutboxPublishActivity(
        string outboxName,
        Guid messageId,
        string? traceParent,
        string? traceState)
    {
        Activity? activity;
        if (ActivityContext.TryParse(traceParent, traceState, true, out var parentContext))
        {
            activity = ActivitySource.StartActivity(
                "outbox publish",
                ActivityKind.Internal,
                parentContext);
        }
        else
        {
            activity = ActivitySource.StartActivity(
                "outbox publish",
                ActivityKind.Internal);
        }

        activity?.SetTag("outbox.name", outboxName);
        activity?.SetTag("messaging.message.id", messageId.ToString());
        return activity;
    }
}

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Shared.Observability;

public class ProjectMetrics
{
    private readonly Counter<long> _outboxPublishAttempts;
    private readonly Histogram<double> _outboxPublishDelay;
    private readonly Counter<long> _reviewJobs;
    private readonly Histogram<double> _reviewJobDuration;
    private readonly Histogram<double> _messageDeliveryDelay;

    public ProjectMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(ProjectTelemetry.MeterName);
        _outboxPublishAttempts = meter.CreateCounter<long>(
            "llmtutorroom.outbox.publish.attempts",
            description: "Number of outbox publication attempts.");
        _outboxPublishDelay = meter.CreateHistogram<double>(
            "llmtutorroom.outbox.publish.delay",
            unit: "s",
            description: "Time from event creation to an outbox publication attempt.");
        _reviewJobs = meter.CreateCounter<long>(
            "llmtutorroom.review.jobs",
            description: "Number of processed review jobs.");
        _reviewJobDuration = meter.CreateHistogram<double>(
            "llmtutorroom.review.job.duration",
            unit: "s",
            description: "Review job processing duration.");
        _messageDeliveryDelay = meter.CreateHistogram<double>(
            "llmtutorroom.messaging.delivery.delay",
            unit: "s",
            description: "Time from message publication to delivery.");
    }

    public void RecordOutboxPublish(
        string outboxName,
        string outcome,
        DateTimeOffset occurredAt)
    {
        var tags = new TagList
        {
            { "outbox.name", outboxName },
            { "outcome", outcome }
        };
        _outboxPublishAttempts.Add(1, tags);
        _outboxPublishDelay.Record(GetElapsedSeconds(occurredAt), tags);
    }

    public void RecordReviewJob(string outcome, TimeSpan duration)
    {
        var tags = new TagList
        {
            { "outcome", outcome }
        };
        _reviewJobs.Add(1, tags);
        _reviewJobDuration.Record(duration.TotalSeconds, tags);
    }

    public void RecordMessageDelivery(string destination, DateTimeOffset publishedAt)
    {
        var tags = new TagList
        {
            { "messaging.destination.name", destination }
        };
        _messageDeliveryDelay.Record(GetElapsedSeconds(publishedAt), tags);
    }

    private static double GetElapsedSeconds(DateTimeOffset startedAt)
    {
        return Math.Max(0, (DateTimeOffset.UtcNow - startedAt).TotalSeconds);
    }
}

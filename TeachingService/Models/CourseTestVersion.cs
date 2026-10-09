using TeachingService.Contracts.Enums;

namespace TeachingService.Models;

public class CourseTestVersion
{
    public Guid Id { get; set; }
    public Guid CourseTestId { get; set; }
    public CourseTest CourseTest { get; set; } = null!;
    public int VersionNumber { get; set; }
    public int ContentRevision { get; set; }
    public CourseTestStatus? VersionSlot { get; private set; } = CourseTestStatus.Draft;
    public CourseTestStatus Status
    {
        get => VersionSlot ?? CourseTestStatus.Superseded;
        set => VersionSlot = value switch
        {
            CourseTestStatus.Draft => CourseTestStatus.Draft,
            CourseTestStatus.Published => CourseTestStatus.Published,
            CourseTestStatus.Superseded => null,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }
    public string Title { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public int TimeLimitMinutes { get; set; }
    public string Summary { get; set; } = string.Empty;
    public int? LlmModelId { get; set; }
    public Guid? FeedbackTemplateId { get; set; }
    public string FeedbackTemplateSnapshotJson { get; set; } = "{}";
    public List<GradingExample> GradingExamples { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PublishedAt { get; set; }
    public List<TestTask> Tasks { get; set; } = new();
}

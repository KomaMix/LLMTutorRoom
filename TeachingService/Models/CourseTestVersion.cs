using TeachingService.Contracts.Enums;

namespace TeachingService.Models
{
    public sealed class CourseTestVersion
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
        public DateTimeOffset Deadline { get; set; }
        public int TimeLimitMinutes { get; set; }
        public string Summary { get; set; } = string.Empty;
        public string LlmModelKey { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? PublishedAt { get; set; }
        public List<TestTask> Tasks { get; set; } = new();
    }
}

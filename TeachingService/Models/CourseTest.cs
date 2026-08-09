using TeachingService.Contracts.Enums;

namespace TeachingService.Models
{
    public sealed class CourseTest
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public CourseTestStatus Status { get; set; } = CourseTestStatus.Draft;
        public DateTimeOffset Deadline { get; set; }
        public int TimeLimitMinutes { get; set; }
        public string Summary { get; set; } = string.Empty;
        public List<TestTask> Tasks { get; set; } = new();
    }
}

using TeachingService.Contracts.Enums;

namespace TeachingService.Contracts.Models
{
    public sealed class CourseTestDto
    {
        public string Id { get; set; } = string.Empty;
        public string TeacherUserId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public CourseTestStatus Status { get; set; } = CourseTestStatus.Draft;
        public DateTimeOffset Deadline { get; set; }
        public int TimeLimitMinutes { get; set; }
        public string Summary { get; set; } = string.Empty;
        public string LlmModelKey { get; set; } = string.Empty;
        public int VersionNumber { get; set; }
        public int ContentRevision { get; set; }
        public int? PublishedVersionNumber { get; set; }
        public bool HasDraft { get; set; }
        public decimal TotalPoints { get; set; }
        public List<TestTaskDto> Tasks { get; set; } = new();
        public List<CourseTestVersionSummaryDto> Versions { get; set; } = new();
    }
}

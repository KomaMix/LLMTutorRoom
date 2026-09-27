using TeachingService.Contracts.Enums;
using TeachingService.Contracts.Models;

namespace TeachingService.Contracts.Requests
{
    public sealed class CreateTestRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public CourseTestStatus Status { get; set; } = CourseTestStatus.Draft;
        public DateTimeOffset? Deadline { get; set; }
        public int TimeLimitMinutes { get; set; } = 45;
        public string LlmModelKey { get; set; } = string.Empty;
        public List<GradingExampleDto> GradingExamples { get; set; } = new();
    }
}

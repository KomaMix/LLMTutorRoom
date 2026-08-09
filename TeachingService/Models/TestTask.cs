using TeachingService.Contracts.Enums;

namespace TeachingService.Models
{
    public sealed class TestTask
    {
        public Guid Id { get; set; }
        public Guid CourseTestId { get; set; }
        public TestTaskType Type { get; set; } = TestTaskType.FreeText;
        public TestTaskCheckMode CheckMode { get; set; } = TestTaskCheckMode.Auto;
        public string Title { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
        public decimal MaxPoints { get; set; }
        public decimal WrongAnswerPenalty { get; set; }
        public bool IsHidden { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public List<AnswerOption> Options { get; set; } = new();
    }
}

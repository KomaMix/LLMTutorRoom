using TeachingService.Contracts.Enums;

namespace TeachingService.Contracts.Models
{
    public sealed class TestTaskDto
    {
        public string Id { get; set; } = string.Empty;
        public TestTaskType Type { get; set; } = TestTaskType.FreeText;
        public TestTaskCheckMode CheckMode { get; set; } = TestTaskCheckMode.Auto;
        public string Title { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
        public decimal MaxPoints { get; set; }
        public decimal WrongAnswerPenalty { get; set; }
        public bool IsHidden { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public List<AnswerOptionDto> Options { get; set; } = new();
        public List<string> CorrectOptionIds { get; set; } = new();
    }
}

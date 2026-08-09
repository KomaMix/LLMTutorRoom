using TeachingService.Contracts.Enums;

namespace TeachingService.Contracts.Requests
{
    public sealed class CreateTaskRequest
    {
        public TestTaskType Type { get; set; } = TestTaskType.FreeText;
        public TestTaskCheckMode? CheckMode { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
        public decimal MaxPoints { get; set; } = 1;
        public decimal WrongAnswerPenalty { get; set; }
        public List<string> Options { get; set; } = new();
        public List<int> CorrectOptionIndexes { get; set; } = new();
    }
}

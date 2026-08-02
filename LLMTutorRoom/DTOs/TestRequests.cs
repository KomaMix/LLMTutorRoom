using LLMTutorRoom.Models;

namespace LLMTutorRoom.DTOs
{
    public sealed class CreateTestRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public CourseTestStatus Status { get; set; } = CourseTestStatus.Draft;
        public DateTimeOffset? Deadline { get; set; }
        public int TimeLimitMinutes { get; set; } = 45;
    }

    public sealed class CreateTaskRequest
    {
        public TestTaskType Type { get; set; } = TestTaskType.FreeText;
        public string Title { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
        public decimal MaxPoints { get; set; } = 1;
        public decimal WrongAnswerPenalty { get; set; }
        public List<string> Options { get; set; } = new();
        public List<int> CorrectOptionIndexes { get; set; } = new();
    }

    public sealed class UpdateTaskVisibilityRequest
    {
        public bool IsHidden { get; set; }
    }

    public sealed class SaveAttemptAnswersRequest
    {
        public Dictionary<string, string> Answers { get; set; } = new();
    }
}

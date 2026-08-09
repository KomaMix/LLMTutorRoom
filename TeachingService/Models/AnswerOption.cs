namespace TeachingService.Models
{
    public sealed class AnswerOption
    {
        public Guid Id { get; set; }
        public Guid TestTaskId { get; set; }
        public string Text { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
    }
}

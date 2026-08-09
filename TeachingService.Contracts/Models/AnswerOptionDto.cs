namespace TeachingService.Contracts.Models
{
    public sealed class AnswerOptionDto
    {
        public string Id { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
    }
}

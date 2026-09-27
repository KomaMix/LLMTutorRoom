namespace TeachingService.Models
{
    public sealed class GradingExample
    {
        public string TaskPrompt { get; set; } = string.Empty;
        public string StudentAnswer { get; set; } = string.Empty;
        public decimal Score { get; set; }
        public decimal MaxScore { get; set; }
        public string Feedback { get; set; } = string.Empty;
    }
}

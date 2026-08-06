namespace LLMTutorRoom.DTOs
{
    public sealed class ManualTaskReviewRequest
    {
        public decimal Score { get; set; }
        public string Feedback { get; set; } = string.Empty;
        public List<string> Findings { get; set; } = new();
    }
}

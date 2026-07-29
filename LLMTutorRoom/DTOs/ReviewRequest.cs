namespace LLMTutorRoom.DTOs
{
    public class ReviewRequest
    {
        public string TestId { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public Dictionary<string, string> Answers { get; set; } = new();
    }
}

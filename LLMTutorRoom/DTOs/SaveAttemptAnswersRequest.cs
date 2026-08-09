namespace LLMTutorRoom.DTOs
{
    public sealed class SaveAttemptAnswersRequest
    {
        public Dictionary<string, string> Answers { get; set; } = new();
    }
}

namespace AttemptService.Contracts.Requests;

public sealed class SaveAttemptAnswersRequest
{
    public Dictionary<string, string> Answers { get; set; } = new();
}

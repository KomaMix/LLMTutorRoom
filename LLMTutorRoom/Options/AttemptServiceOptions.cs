namespace LLMTutorRoom.Options;

public sealed class AttemptServiceOptions
{
    public const string SectionName = "AttemptService";

    public string BaseUrl { get; set; } = "http://localhost:5216";
    public int RequestTimeoutSeconds { get; set; } = 15;
}

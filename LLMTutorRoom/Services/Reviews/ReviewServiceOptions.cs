namespace LLMTutorRoom.Services.Reviews;

public sealed class ReviewServiceOptions
{
    public const string SectionName = "ReviewService";

    public string BaseUrl { get; set; } = "http://127.0.0.1:5214";
    public int RequestTimeoutSeconds { get; set; } = 15;
}

namespace LLMTutorRoom.Services.Groups;

public class GroupServiceOptions
{
    public const string SectionName = "GroupService";

    public string BaseUrl { get; set; } = "http://group-service:8080";
    public int RequestTimeoutSeconds { get; set; } = 15;
}

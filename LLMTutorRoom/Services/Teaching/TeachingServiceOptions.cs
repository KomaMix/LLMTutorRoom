namespace LLMTutorRoom.Services.Teaching
{
    public sealed class TeachingServiceOptions
    {
        public const string SectionName = "TeachingService";

        public string BaseUrl { get; set; } = "http://localhost:5212";
        public int RequestTimeoutSeconds { get; set; } = 15;
    }
}

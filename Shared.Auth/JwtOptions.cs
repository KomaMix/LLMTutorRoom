namespace Shared.Auth
{
    public sealed class JwtOptions
    {
        public string Issuer { get; set; } = "LLMTutorRoom";
        public string Audience { get; set; } = "LLMTutorRoom.Client";
        public string SigningKey { get; set; } = string.Empty;
        public int TokenLifetimeMinutes { get; set; } = 480;
    }
}

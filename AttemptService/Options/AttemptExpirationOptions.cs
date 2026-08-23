namespace AttemptService.Options;

public sealed class AttemptExpirationOptions
{
    public const string SectionName = "AttemptExpiration";

    public bool Enabled { get; set; } = true;
    public int PollIntervalSeconds { get; set; } = 5;
    public int BatchSize { get; set; } = 200;
}

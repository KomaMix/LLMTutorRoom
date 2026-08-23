namespace AttemptService.Options;

public sealed class AttemptOutboxOptions
{
    public const string SectionName = "AttemptOutbox";

    public bool Enabled { get; set; } = true;
    public int PollIntervalSeconds { get; set; } = 5;
    public int RetryDelaySeconds { get; set; } = 15;
    public int BatchSize { get; set; } = 25;
    public int PublishedMessageRetentionDays { get; set; } = 30;
    public int CleanupIntervalMinutes { get; set; } = 60;
    public int CleanupBatchSize { get; set; } = 500;
}

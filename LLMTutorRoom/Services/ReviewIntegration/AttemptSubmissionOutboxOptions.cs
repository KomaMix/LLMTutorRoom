namespace LLMTutorRoom.Services.ReviewIntegration;

public sealed class AttemptSubmissionOutboxOptions
{
    public const string SectionName = "AttemptSubmissionOutbox";

    public bool Enabled { get; set; } = true;
    public int PollIntervalSeconds { get; set; } = 5;
    public int RetryDelaySeconds { get; set; } = 15;
    public int BatchSize { get; set; } = 25;
    public int PublishedMessageRetentionDays { get; set; } = 30;
    public int CleanupIntervalMinutes { get; set; } = 60;
    public int CleanupBatchSize { get; set; } = 500;
}

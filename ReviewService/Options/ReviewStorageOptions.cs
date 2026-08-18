namespace ReviewService.Options;

public sealed class ReviewStorageOptions
{
    public const string SectionName = "ReviewStorage";

    public int InboxRetentionDays { get; set; } = 90;
    public int CleanupIntervalMinutes { get; set; } = 60;
    public int CleanupBatchSize { get; set; } = 1000;
    public int PendingSubmissionWarningHours { get; set; } = 24;
}

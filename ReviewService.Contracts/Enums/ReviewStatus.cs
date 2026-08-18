namespace ReviewService.Contracts.Enums;

public enum ReviewStatus
{
    Checked,
    Queued,
    Processing,
    RetryScheduled,
    PendingPolicy,
    ManualReview,
    Failed
}

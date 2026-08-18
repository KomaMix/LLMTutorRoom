namespace ReviewService.Contracts.Enums;

public enum ReviewTaskStatus
{
    Pending,
    Processing,
    Succeeded,
    RetryScheduled,
    ManualReview,
    Failed
}

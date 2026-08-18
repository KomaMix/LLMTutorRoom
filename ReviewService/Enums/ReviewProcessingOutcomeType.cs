namespace ReviewService.Enums;

public enum ReviewProcessingOutcomeType
{
    Ignored,
    Completed,
    RetryScheduled,
    Poison
}

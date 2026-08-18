using ReviewService.Enums;

namespace ReviewService.Models.Reviews;

public sealed record ReviewProcessingOutcome(
    ReviewProcessingOutcomeType Type,
    int? RetryDelaySeconds = null)
{
    public static ReviewProcessingOutcome Ignored() => new(ReviewProcessingOutcomeType.Ignored);
    public static ReviewProcessingOutcome Completed() => new(ReviewProcessingOutcomeType.Completed);
    public static ReviewProcessingOutcome RetryScheduled(int delaySeconds) =>
        new(ReviewProcessingOutcomeType.RetryScheduled, delaySeconds);
    public static ReviewProcessingOutcome Poison() => new(ReviewProcessingOutcomeType.Poison);
}

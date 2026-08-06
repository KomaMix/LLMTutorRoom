namespace LLMTutorRoom.Services.ReviewProcessing
{
    public enum ReviewProcessingOutcomeType
    {
        Ignored,
        Completed,
        RetryScheduled,
        Poison
    }

    public sealed class ReviewProcessingOutcome
    {
        public ReviewProcessingOutcomeType Type { get; init; }
        public int? RetryDelaySeconds { get; init; }

        public static ReviewProcessingOutcome Ignored() => new()
        {
            Type = ReviewProcessingOutcomeType.Ignored
        };

        public static ReviewProcessingOutcome Completed() => new()
        {
            Type = ReviewProcessingOutcomeType.Completed
        };

        public static ReviewProcessingOutcome RetryScheduled(int retryDelaySeconds) => new()
        {
            Type = ReviewProcessingOutcomeType.RetryScheduled,
            RetryDelaySeconds = retryDelaySeconds
        };

        public static ReviewProcessingOutcome Poison() => new()
        {
            Type = ReviewProcessingOutcomeType.Poison
        };
    }
}

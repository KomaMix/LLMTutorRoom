namespace ReviewService.Models.Reviews;

public sealed record ReviewQueueMessage(
    int ReviewId,
    int AttemptId,
    string ModelKey,
    DateTimeOffset RequestedAt);

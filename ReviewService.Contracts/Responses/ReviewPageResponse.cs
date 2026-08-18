namespace ReviewService.Contracts.Responses;

public sealed record ReviewPageResponse(
    List<ReviewResponse> NonTerminalReviews,
    List<ReviewResponse> TerminalReviews,
    string? NextCursor,
    ReviewAggregateResponse Aggregate);

public sealed record ReviewAggregateResponse(
    int PendingReviews,
    decimal AverageScorePercentage);

public static class ReviewPagination
{
    public const int DefaultPageSize = 25;
    public const int MaximumPageSize = 100;
}

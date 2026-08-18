namespace ReviewService.Contracts.Messaging;

public static class ReviewIntegrationRoutes
{
    public const string ExchangeName = "review.integration";
    public const string TestReviewPolicyPublishedV1 = "test-review-policy.published.v1";
    public const string AttemptSubmittedV1 = "attempt.submitted.v1";
}

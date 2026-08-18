using ReviewService.Contracts.Enums;
using ReviewService.Contracts.Events;
using ReviewService.Models.Reviews;

namespace ReviewService.Interfaces;

public interface IReviewScoringService
{
    ReviewTask CreateInitialResult(
        ReviewTaskPolicySnapshot task,
        string studentAnswer,
        DateTimeOffset now);

    void ApplyLlmResult(
        ReviewTask existingResult,
        LlmTaskReviewResult llmResult,
        DateTimeOffset now);

    void RecalculateReview(Review review);
    ReviewStatus GetReviewStatusAfterTaskProcessing(Review review);
    string CreateSummary(decimal score, decimal maxScore);
}

using ReviewService.Models.Reviews;
using ReviewService.Contracts.Events;

namespace ReviewService.Interfaces;

public interface ILlmGatewayReviewClient
{
    Task<LlmTaskReviewResult> ReviewFreeTextAnswerAsync(
        int modelId,
        ReviewTask task,
        IReadOnlyList<GradingExampleSnapshot> testGradingExamples,
        CancellationToken cancellationToken);
}

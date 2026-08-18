using ReviewService.Models.Reviews;

namespace ReviewService.Interfaces;

public interface ILlmGatewayReviewClient
{
    Task<LlmTaskReviewResult> ReviewFreeTextAnswerAsync(
        string modelKey,
        ReviewTask task,
        CancellationToken cancellationToken);
}

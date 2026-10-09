using ReviewService.Models.Reviews;
using ReviewService.Contracts.Events;

namespace ReviewService.Interfaces;

public interface ILlmGatewayReviewClient
{
    Task<LlmTaskReviewResult> ReviewFreeTextAnswerAsync(
        int modelId,
        ReviewTask task,
        IList<GradingExampleSnapshot> testGradingExamples,
        FeedbackTemplateSnapshot feedbackTemplate,
        CancellationToken cancellationToken);
}

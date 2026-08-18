using ReviewService.Models.Reviews;

namespace ReviewService.Interfaces;

public interface IReviewJobProcessor
{
    Task<ReviewProcessingOutcome> ProcessAsync(
        ReviewQueueMessage message,
        CancellationToken cancellationToken);
}

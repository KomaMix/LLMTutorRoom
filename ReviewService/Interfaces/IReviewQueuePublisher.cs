using ReviewService.Models.Reviews;

namespace ReviewService.Interfaces;

public interface IReviewQueuePublisher
{
    Task PublishAsync(
        ReviewQueueMessage message,
        int? retryDelaySeconds,
        CancellationToken cancellationToken);
}

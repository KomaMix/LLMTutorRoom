namespace LLMTutorRoom.Services.ReviewProcessing
{
    public interface IReviewQueuePublisher
    {
        Task PublishAsync(
            ReviewQueueMessage message,
            int? retryDelaySeconds,
            CancellationToken cancellationToken);
    }
}

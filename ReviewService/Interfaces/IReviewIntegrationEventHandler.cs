using ReviewService.Contracts.Events;

namespace ReviewService.Interfaces;

public interface IReviewIntegrationEventHandler
{
    Task HandleAsync(TestReviewPolicyPublishedV1 message, CancellationToken cancellationToken);
    Task HandleAsync(AttemptSubmittedV1 message, CancellationToken cancellationToken);
}

using TeachingService.Models;

namespace TeachingService.Interfaces
{
    public interface IIntegrationEventPublisher
    {
        Task PublishAsync(
            IntegrationOutboxMessage message,
            CancellationToken cancellationToken);
    }
}

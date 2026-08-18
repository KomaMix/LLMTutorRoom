using ReviewService.Contracts.Responses;

namespace ReviewService.Interfaces;

public interface ILlmGatewayModelCatalogClient
{
    Task<List<LlmModelCatalogItemResponse>> GetModelsAsync(
        CancellationToken cancellationToken);
}

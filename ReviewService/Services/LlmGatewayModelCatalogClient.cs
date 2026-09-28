using System.Net.Http.Json;
using ReviewService.Contracts.Responses;
using ReviewService.Helpers;
using ReviewService.Interfaces;

namespace ReviewService.Services;

public sealed class LlmGatewayModelCatalogClient(HttpClient httpClient) : ILlmGatewayModelCatalogClient
{
    public async Task<List<LlmModelCatalogItemResponse>> GetModelsAsync(CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync("/internal/models/catalog", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<LlmModelCatalogItemResponse>>(JsonHelper.Options, cancellationToken) ?? [];
    }
}

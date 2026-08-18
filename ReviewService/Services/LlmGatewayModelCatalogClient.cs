using System.Net.Http.Json;
using ReviewService.Contracts.Responses;
using ReviewService.Helpers;
using ReviewService.Interfaces;

namespace ReviewService.Services;

public sealed class LlmGatewayModelCatalogClient(HttpClient httpClient)
    : ILlmGatewayModelCatalogClient
{
    public async Task<List<LlmModelCatalogItemResponse>> GetModelsAsync(
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync("/api/models/catalog", cancellationToken);
        response.EnsureSuccessStatusCode();
        var models = await response.Content.ReadFromJsonAsync<List<LlmGatewayModelResponse>>(
                JsonHelper.Options,
                cancellationToken)
            ?? [];

        return models
            .Select(model => new LlmModelCatalogItemResponse(
                model.Key,
                string.IsNullOrWhiteSpace(model.DisplayName) ? model.Key : model.DisplayName,
                model.Deployments.Any(deployment => deployment.IsEnabled)))
            .OrderBy(model => model.Key, StringComparer.Ordinal)
            .ToList();
    }

    private sealed record LlmGatewayModelResponse(
        string Key,
        string DisplayName,
        List<LlmGatewayDeploymentResponse> Deployments);

    private sealed record LlmGatewayDeploymentResponse(bool IsEnabled);
}

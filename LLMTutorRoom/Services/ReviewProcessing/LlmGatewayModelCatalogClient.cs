using System.Net.Http.Json;
using System.Text.Json;
using LLMTutorRoom.DTOs;

namespace LLMTutorRoom.Services.ReviewProcessing
{
    public sealed class LlmGatewayModelCatalogClient
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly HttpClient _httpClient;

        public LlmGatewayModelCatalogClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<LlmModelCatalogItemResponse>> GetModelsAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                using var response = await _httpClient.GetAsync("/api/models/catalog", cancellationToken);
                response.EnsureSuccessStatusCode();

                var models = await response.Content.ReadFromJsonAsync<List<LlmGatewayModelResponse>>(
                        JsonOptions,
                        cancellationToken)
                    ?? new List<LlmGatewayModelResponse>();

                return models
                    .Select(model => new LlmModelCatalogItemResponse(
                        model.Key,
                        string.IsNullOrWhiteSpace(model.DisplayName) ? model.Key : model.DisplayName,
                        model.Deployments.Any(deployment => deployment.IsEnabled)))
                    .OrderBy(model => model.Key)
                    .ToList();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return new List<LlmModelCatalogItemResponse>();
            }
        }

        private sealed record LlmGatewayModelResponse(
            string Key,
            string DisplayName,
            IReadOnlyCollection<LlmGatewayDeploymentResponse> Deployments);

        private sealed record LlmGatewayDeploymentResponse(bool IsEnabled);
    }
}

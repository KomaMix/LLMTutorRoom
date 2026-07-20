using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace LLMGateway.Tests
{
    public class ModelEndpointsTests : IClassFixture<TestWebApplicationFactory>
    {
        private readonly HttpClient _client;

        public ModelEndpointsTests(TestWebApplicationFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task GetModels_ReturnsOnlyKeysOfModelsWithEnabledDeployments()
        {
            var modelKey = await CreateModelAsync();
            await CreateDeploymentAsync(modelKey, isEnabled: true);

            var response = await _client.GetAsync("/api/models");

            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Contains(
                document.RootElement.EnumerateArray(),
                item => item.ValueKind == JsonValueKind.String && item.GetString() == modelKey);
        }

        [Fact]
        public async Task UpdateDeployment_ReplacesItsConfiguration()
        {
            var modelKey = await CreateModelAsync();
            var deploymentId = await CreateDeploymentAsync(modelKey, isEnabled: true);

            var response = await _client.PutAsJsonAsync(
                $"/api/models/deployments/{deploymentId}",
                new
                {
                    providerType = "OpenAiCompatible",
                    endpoint = "https://example.test/v1",
                    apiKey = "test-key",
                    providerModelId = "remote-model",
                    isEnabled = false,
                    priority = 10
                });

            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("OpenAiCompatible", document.RootElement.GetProperty("providerType").GetString());
            Assert.Equal("remote-model", document.RootElement.GetProperty("providerModelId").GetString());
            Assert.Equal(10, document.RootElement.GetProperty("priority").GetInt32());
            Assert.False(document.RootElement.GetProperty("isEnabled").GetBoolean());
            Assert.False(document.RootElement.TryGetProperty("apiKey", out _));
        }

        [Fact]
        public async Task DeleteRateLimitRule_RemovesTheRule()
        {
            var modelKey = await CreateModelAsync();
            var deploymentId = await CreateDeploymentAsync(modelKey, isEnabled: true);
            var createResponse = await _client.PostAsync(
                $"/api/models/deployments/{deploymentId}/rate-limits",
                CreateJsonContent("""{ "windowSeconds": 60, "maxRequests": 5 }"""));

            createResponse.EnsureSuccessStatusCode();
            using var createdRule = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
            var ruleId = createdRule.RootElement.GetProperty("id").GetInt32();

            var deleteResponse = await _client.DeleteAsync($"/api/models/rate-limits/{ruleId}");
            var modelResponse = await _client.GetAsync($"/api/models/{modelKey}");

            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
            modelResponse.EnsureSuccessStatusCode();
            using var model = JsonDocument.Parse(await modelResponse.Content.ReadAsStringAsync());
            var rules = model.RootElement
                .GetProperty("deployments")[0]
                .GetProperty("rateLimitRules");
            Assert.Equal(0, rules.GetArrayLength());
        }

        private async Task<string> CreateModelAsync()
        {
            var modelKey = $"test-model-{Guid.NewGuid():N}";
            var response = await _client.PostAsJsonAsync("/api/models", new
            {
                key = modelKey,
                displayName = "Test model",
                description = "Integration test model"
            });

            response.EnsureSuccessStatusCode();
            return modelKey;
        }

        private async Task<int> CreateDeploymentAsync(string modelKey, bool isEnabled)
        {
            var response = await _client.PostAsJsonAsync(
                $"/api/models/{modelKey}/deployments",
                new
                {
                    providerType = "Ollama",
                    endpoint = "http://localhost:11434",
                    providerModelId = modelKey,
                    isEnabled,
                    priority = 0
                });

            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return document.RootElement.GetProperty("id").GetInt32();
        }

        private static StringContent CreateJsonContent(string content)
        {
            return new StringContent(content, Encoding.UTF8, "application/json");
        }
    }
}

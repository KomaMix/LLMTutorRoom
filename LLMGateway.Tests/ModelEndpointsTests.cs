using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LLMGateway.Data;
using LLMGateway.DTOs.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LLMGateway.Tests;

public sealed class ModelEndpointsTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task Administration_RequiresAdmin()
    {
        using var anonymous = factory.CreateClient();
        using var teacher = factory.CreateAuthenticatedClient("Teacher");
        using var admin = factory.CreateAuthenticatedClient("Admin");
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/models")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.GetAsync("/api/models")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/models")).StatusCode);
    }

    [Fact]
    public async Task InternalCatalog_DoesNotRequireAuthenticationAndOmitsDeploymentSettings()
    {
        using var admin = factory.CreateAuthenticatedClient("Admin");
        using var anonymous = factory.CreateClient();
        var model = await CreateModelAsync(admin);
        await CreateDeploymentAsync(admin, model.Id);
        var response = await anonymous.GetAsync("/internal/models/catalog");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var item = document.RootElement.EnumerateArray().Single(value => value.GetProperty("id").GetInt32() == model.Id);
        Assert.True(item.GetProperty("hasEnabledDeployment").GetBoolean());
        Assert.Equal(model.Key, item.GetProperty("key").GetString());
        Assert.False(item.TryGetProperty("deployments", out _));
    }

    [Fact]
    public async Task InternalChat_DoesNotRequireAuthentication()
    {
        using var anonymous = factory.CreateClient();
        using var response = await anonymous.PostAsJsonAsync("/internal/chat/2147483647", new
        {
            messages = new[] { new { role = "user", content = "hi" } }
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Model not found", document.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task RenameAndArchive_PreserveModelIdentityAndDeployments()
    {
        using var client = factory.CreateAuthenticatedClient("Admin");
        var model = await CreateModelAsync(client);
        var deployment = await CreateDeploymentAsync(client, model.Id);
        var response = await client.PutAsJsonAsync($"/api/models/{model.Id}", new UpdateModelRequest
        {
            Key = "renamed-" + model.Key, DisplayName = "Renamed model", IsArchived = true
        });
        response.EnsureSuccessStatusCode();
        var updated = (await response.Content.ReadFromJsonAsync<ModelResponse>())!;
        Assert.Equal(model.Id, updated.Id);
        Assert.Equal("renamed-" + model.Key, updated.Key);
        Assert.True(updated.IsArchived);
        Assert.Equal(deployment.Id, Assert.Single(updated.Deployments).Id);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/models/{model.Key}")).StatusCode);
    }

    [Fact]
    public async Task DeploymentSecret_IsPreservedReplacedAndExplicitlyCleared()
    {
        using var client = factory.CreateAuthenticatedClient("Admin");
        var model = await CreateModelAsync(client);
        var deployment = await CreateDeploymentAsync(client, model.Id);
        var request = new UpdateModelDeploymentRequest
        {
            Name = "Updated deployment", Endpoint = "https://provider.test/v1", ProviderModelId = "provider-model",
            IsEnabled = false, Priority = 10, MaxConcurrentRequests = 3
        };
        var response = await client.PutAsJsonAsync($"/api/models/deployments/{deployment.Id}", request);
        response.EnsureSuccessStatusCode();
        var updated = (await response.Content.ReadFromJsonAsync<ModelDeploymentResponse>())!;
        Assert.True(updated.HasApiKey);
        Assert.False(updated.IsEnabled);
        Assert.Equal(10, updated.Priority);
        Assert.Equal(3, updated.MaxConcurrentRequests);
        Assert.Equal("initial-test-secret", await ReadSecretAsync(deployment.Id));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(body.RootElement.TryGetProperty("apiKey", out _));

        (await client.PutAsJsonAsync($"/api/models/deployments/{deployment.Id}", request with { ApiKey = "replacement" })).EnsureSuccessStatusCode();
        Assert.Equal("replacement", await ReadSecretAsync(deployment.Id));
        var clear = await client.PutAsJsonAsync($"/api/models/deployments/{deployment.Id}", request with { ClearApiKey = true });
        clear.EnsureSuccessStatusCode();
        Assert.False((await clear.Content.ReadFromJsonAsync<ModelDeploymentResponse>())!.HasApiKey);
        Assert.Null(await ReadSecretAsync(deployment.Id));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/models/deployments/{deployment.Id}",
            request with { ApiKey = "ambiguous", ClearApiKey = true })).StatusCode);
    }

    [Fact]
    public async Task RateLimits_CanBeCreatedUpdatedAndDeletedWithoutReplacingOtherRules()
    {
        using var client = factory.CreateAuthenticatedClient("Admin");
        var model = await CreateModelAsync(client);
        var deployment = await CreateDeploymentAsync(client, model.Id);
        var first = await client.PostAsJsonAsync($"/api/models/deployments/{deployment.Id}/rate-limits", new CreateRateLimitRuleRequest { WindowSeconds = 60, MaxRequests = 5 });
        var second = await client.PostAsJsonAsync($"/api/models/deployments/{deployment.Id}/rate-limits", new CreateRateLimitRuleRequest { WindowSeconds = 3600, MaxRequests = 100 });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.NotNull(first.Headers.Location);
        second.EnsureSuccessStatusCode();
        var firstRule = (await first.Content.ReadFromJsonAsync<RateLimitRuleResponse>())!;
        var secondRule = (await second.Content.ReadFromJsonAsync<RateLimitRuleResponse>())!;
        Assert.NotEqual(firstRule.Id, secondRule.Id);
        var update = await client.PutAsJsonAsync($"/api/models/rate-limits/{firstRule.Id}", new UpdateRateLimitRuleRequest { WindowSeconds = 120, MaxRequests = 7 });
        update.EnsureSuccessStatusCode();
        Assert.Equal(new RateLimitRuleResponse(firstRule.Id, deployment.Id, 120, 7), await update.Content.ReadFromJsonAsync<RateLimitRuleResponse>());
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/models/rate-limits/{firstRule.Id}")).StatusCode);
        var saved = (await client.GetFromJsonAsync<ModelDeploymentResponse>($"/api/models/deployments/{deployment.Id}"))!;
        Assert.Equal(secondRule, Assert.Single(saved.RateLimitRules));
    }

    [Fact]
    public async Task DeleteDeployment_RemovesItsRulesAndLeavesTheModel()
    {
        using var client = factory.CreateAuthenticatedClient("Admin");
        var model = await CreateModelAsync(client);
        var deployment = await CreateDeploymentAsync(client, model.Id);
        var creation = await client.PostAsJsonAsync($"/api/models/deployments/{deployment.Id}/rate-limits", new CreateRateLimitRuleRequest { WindowSeconds = 60, MaxRequests = 5 });
        creation.EnsureSuccessStatusCode();
        var rule = (await creation.Content.ReadFromJsonAsync<RateLimitRuleResponse>())!;
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/models/deployments/{deployment.Id}")).StatusCode);
        var saved = await client.GetFromJsonAsync<ModelResponse>($"/api/models/{model.Id}");
        Assert.Empty(saved!.Deployments);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/models/rate-limits/{rule.Id}")).StatusCode);
    }

    [Theory]
    [InlineData("ftp://provider.test/v1")]
    [InlineData("https://user:password@provider.test/v1")]
    [InlineData("https://provider.test/v1?api_key=secret")]
    public async Task Deployment_RejectsUnsupportedEndpoints(string endpoint)
    {
        using var client = factory.CreateAuthenticatedClient("Admin");
        var model = await CreateModelAsync(client);
        var response = await client.PostAsJsonAsync($"/api/models/{model.Id}/deployments",
            new CreateModelDeploymentRequest { Name = "Invalid", Endpoint = endpoint, ProviderModelId = "provider-model" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CheckDeployment_ReturnsTheProbeResult()
    {
        using var client = factory.CreateAuthenticatedClient("Admin");
        var model = await CreateModelAsync(client);
        var deployment = await CreateDeploymentAsync(client, model.Id);
        var response = await client.PostAsync($"/api/models/deployments/{deployment.Id}/check", null);
        response.EnsureSuccessStatusCode();
        var result = (await response.Content.ReadFromJsonAsync<DeploymentCheckResponse>())!;
        Assert.True(result.IsAvailable);
        Assert.Equal("Model found.", result.Message);
    }

    private static async Task<ModelResponse> CreateModelAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/models", new CreateModelRequest { Key = $"model-{Guid.NewGuid():N}", DisplayName = "Test model" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        return (await response.Content.ReadFromJsonAsync<ModelResponse>())!;
    }

    private static async Task<ModelDeploymentResponse> CreateDeploymentAsync(HttpClient client, int modelId)
    {
        var response = await client.PostAsJsonAsync($"/api/models/{modelId}/deployments", new CreateModelDeploymentRequest
        {
            Name = "Test deployment", Endpoint = "http://provider.test/v1", ProviderModelId = "provider-model", ApiKey = "initial-test-secret"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ModelDeploymentResponse>())!;
    }

    private async Task<string?> ReadSecretAsync(int id)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().ModelDeployments
            .Where(item => item.Id == id).Select(item => item.ApiKey).SingleAsync();
    }
}

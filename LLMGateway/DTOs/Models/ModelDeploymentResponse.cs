namespace LLMGateway.DTOs.Models;

public sealed record ModelDeploymentResponse(
    int Id, int ModelId, string Name, string Endpoint, string ProviderModelId,
    bool HasApiKey, bool IsEnabled, int Priority, int? MaxConcurrentRequests,
    DateTimeOffset UpdatedAt, List<RateLimitRuleResponse> RateLimitRules);

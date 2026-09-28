using LLMGateway.DTOs.Models;

namespace LLMGateway.Services;

public interface IModelManagementService
{
    Task<List<ModelResponse>> GetModelsAsync(CancellationToken ct);
    Task<List<ModelCatalogItemResponse>> GetCatalogAsync(CancellationToken ct);
    Task<ModelResponse?> GetModelAsync(int modelId, CancellationToken ct);
    Task<ModelResponse> CreateModelAsync(CreateModelRequest request, CancellationToken ct);
    Task<ModelResponse?> UpdateModelAsync(int modelId, UpdateModelRequest request, CancellationToken ct);
    Task<ModelDeploymentResponse?> GetDeploymentAsync(int deploymentId, CancellationToken ct);
    Task<ModelDeploymentResponse> CreateDeploymentAsync(int modelId, CreateModelDeploymentRequest request, CancellationToken ct);
    Task<ModelDeploymentResponse?> UpdateDeploymentAsync(int deploymentId, UpdateModelDeploymentRequest request, CancellationToken ct);
    Task<bool> DeleteDeploymentAsync(int deploymentId, CancellationToken ct);
    Task<DeploymentCheckResponse?> CheckDeploymentAsync(int deploymentId, CancellationToken ct);
    Task<RateLimitRuleResponse?> GetRateLimitAsync(int ruleId, CancellationToken ct);
    Task<RateLimitRuleResponse> CreateRateLimitAsync(int deploymentId, CreateRateLimitRuleRequest request, CancellationToken ct);
    Task<RateLimitRuleResponse?> UpdateRateLimitAsync(int ruleId, UpdateRateLimitRuleRequest request, CancellationToken ct);
    Task<bool> DeleteRateLimitAsync(int ruleId, CancellationToken ct);
}

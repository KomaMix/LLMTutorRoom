namespace LLMGateway.DTOs.Models;

public sealed record ModelResponse(
    int Id, string Key, string DisplayName, string? Description, bool IsArchived,
    List<ModelDeploymentResponse> Deployments);

public sealed record ModelCatalogItemResponse(
    int Id, string Key, string DisplayName, bool IsArchived, bool HasEnabledDeployment);

namespace ReviewService.Contracts.Responses;

public sealed record LlmModelCatalogItemResponse(
    string Key,
    string DisplayName,
    bool HasEnabledDeployment);

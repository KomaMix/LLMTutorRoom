namespace ReviewService.Contracts.Responses;

public sealed record LlmModelCatalogItemResponse(
    int Id, string Key, string DisplayName, bool IsArchived, bool HasEnabledDeployment);

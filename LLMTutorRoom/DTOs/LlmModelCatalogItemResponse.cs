namespace LLMTutorRoom.DTOs
{
    public sealed record LlmModelCatalogItemResponse(
        string Key,
        string DisplayName,
        bool HasEnabledDeployment);
}

namespace LLMGateway.DTOs
{
    public class ModelResponse
    {
        public string Key { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public IReadOnlyCollection<ModelDeploymentResponse> Deployments { get; set; } = Array.Empty<ModelDeploymentResponse>();
    }
}

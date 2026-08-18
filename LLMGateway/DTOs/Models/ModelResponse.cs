namespace LLMGateway.DTOs.Models
{
    public class ModelResponse
    {
        public string Key { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<ModelDeploymentResponse> Deployments { get; set; } = new();
    }
}

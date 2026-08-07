namespace LLMGateway.DTOs.Models
{
    public class ModelDeploymentResponse
    {
        public int Id { get; set; }
        public string Endpoint { get; set; } = string.Empty;
        public string ProviderModelId { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public int Priority { get; set; }
        public int? MaxConcurrentRequests { get; set; }
        public IReadOnlyCollection<RateLimitRuleResponse> RateLimitRules { get; set; } = Array.Empty<RateLimitRuleResponse>();
    }
}

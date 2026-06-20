namespace LLMGateway.Data.Models
{
    public class ModelDeployment
    {
        public int Id { get; set; }
        public int ModelId { get; set; }
        public Model Model { get; set; } = null!;
        public string ProviderType { get; set; } = string.Empty;
        public string Endpoint { get; set; } = string.Empty;
        public string? ApiKey { get; set; }
        public string ProviderModelId { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public int Priority { get; set; }
        public int MaxConcurrentRequests { get; set; } = 1;
        public int CurrentRequestCount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public ICollection<ModelRateLimitRule> RateLimitRules { get; set; } = new List<ModelRateLimitRule>();
    }
}

namespace LLMGateway.Data.Models
{
    public class ModelRateLimitBucket
    {
        public int ModelRateLimitRuleId { get; set; }
        public ModelRateLimitRule ModelRateLimitRule { get; set; } = null!;
        public DateTime WindowStartedAt { get; set; }
        public int RequestCount { get; set; }
    }
}

namespace LLMGateway.Data.Models
{
    public class ModelRateLimitRule
    {
        public int Id { get; set; }
        public int DeploymentId { get; set; }
        public ModelDeployment Deployment { get; set; } = null!;
        public int WindowSeconds { get; set; }
        public int MaxRequests { get; set; }
    }
}

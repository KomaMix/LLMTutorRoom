namespace LLMGateway.Data.Models
{
    public class ModelRateLimitRule
    {
        public int Id { get; set; }
        public int WindowSeconds { get; set; }
        public int MaxRequests { get; set; }
    }
}

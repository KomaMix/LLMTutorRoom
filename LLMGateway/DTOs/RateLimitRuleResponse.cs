namespace LLMGateway.DTOs
{
    public class RateLimitRuleResponse
    {
        public int Id { get; set; }
        public int WindowSeconds { get; set; }
        public int MaxRequests { get; set; }
    }
}

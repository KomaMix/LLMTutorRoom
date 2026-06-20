using System.ComponentModel.DataAnnotations;

namespace LLMGateway.DTOs
{
    public class CreateRateLimitRuleRequest
    {
        [Range(1, 86400)]
        public int WindowSeconds { get; set; }

        [Range(1, 1000000)]
        public int MaxRequests { get; set; }
    }
}

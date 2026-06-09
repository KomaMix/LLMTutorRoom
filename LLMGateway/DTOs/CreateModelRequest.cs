using System.ComponentModel.DataAnnotations;

namespace LLMGateway.DTOs
{
    public class CreateModelRequest
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Type { get; set; } = string.Empty;
        public string Endpoint { get; set; } = string.Empty;
        public string? ApiKey { get; set; }
        public string ModelId { get; set; } = string.Empty;
        public int RateLimitPermit { get; set; }
        public int RateLimitWindowSeconds { get; set; }
        public bool IsActive { get; set; }
    }
}

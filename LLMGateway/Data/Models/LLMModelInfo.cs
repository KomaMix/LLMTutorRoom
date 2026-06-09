using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LLMGateway.Data.Models
{
    public class LLMModelInfo
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Endpoint { get; set; } = string.Empty;
        public string? ApiKey { get; set; }
        public string ModelId { get; set; } = string.Empty;
        public int RateLimitPermit { get; set; } = 10;
        public int RateLimitWindowSeconds { get; set; } = 60;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}

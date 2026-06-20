using System.ComponentModel.DataAnnotations;

namespace LLMGateway.DTOs
{
    public class CreateModelDeploymentRequest
    {
        [Required, MaxLength(100)]
        public string ProviderType { get; set; } = string.Empty;

        [Required, Url, MaxLength(2000)]
        public string Endpoint { get; set; } = string.Empty;

        public string? ApiKey { get; set; }

        [Required, MaxLength(200)]
        public string ProviderModelId { get; set; } = string.Empty;

        public bool IsEnabled { get; set; } = true;
        public int Priority { get; set; }

        [Range(1, 1000)]
        public int MaxConcurrentRequests { get; set; } = 1;
    }
}

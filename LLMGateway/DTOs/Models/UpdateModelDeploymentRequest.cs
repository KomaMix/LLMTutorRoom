using System.ComponentModel.DataAnnotations;

namespace LLMGateway.DTOs.Models
{
    public class UpdateModelDeploymentRequest
    {
        public string Endpoint { get; set; } = string.Empty;

        public string? ApiKey { get; set; }

        public string ProviderModelId { get; set; } = string.Empty;

        public bool IsEnabled { get; set; }
        public int Priority { get; set; }

        [Range(0, int.MaxValue)]
        public int? MaxConcurrentRequests { get; set; }
    }
}

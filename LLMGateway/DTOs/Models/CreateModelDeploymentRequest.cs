using System.ComponentModel.DataAnnotations;
using LLMGateway.Data.Models;

namespace LLMGateway.DTOs.Models
{
    public class CreateModelDeploymentRequest
    {
        [Required, EnumDataType(typeof(ModelProviderType))]
        public ModelProviderType? ProviderType { get; set; }

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

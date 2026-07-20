using System.ComponentModel.DataAnnotations;
using LLMGateway.Data.Models;

namespace LLMGateway.DTOs.Models
{
    public class UpdateModelDeploymentRequest
    {
        [Required, EnumDataType(typeof(ModelProviderType))]
        public ModelProviderType? ProviderType { get; set; }

        [Required, Url, MaxLength(2000)]
        public string Endpoint { get; set; } = string.Empty;

        public string? ApiKey { get; set; }

        [Required, MaxLength(200)]
        public string ProviderModelId { get; set; } = string.Empty;

        public bool IsEnabled { get; set; }
        public int Priority { get; set; }

        [Range(0, int.MaxValue)]
        public int? MaxConcurrentRequests { get; set; }
    }
}

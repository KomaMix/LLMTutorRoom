using System.ComponentModel.DataAnnotations;

namespace LLMGateway.DTOs.Models
{
    public class CreateModelRequest
    {
        [Required, MaxLength(200)]
        public string Key { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string DisplayName { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }
    }
}

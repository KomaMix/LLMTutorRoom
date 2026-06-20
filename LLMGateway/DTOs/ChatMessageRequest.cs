using System.ComponentModel.DataAnnotations;

namespace LLMGateway.DTOs
{
    public class ChatMessageRequest
    {
        [Required]
        [RegularExpression("^(system|user|assistant)$", ErrorMessage = "Role must be system, user, or assistant.")]
        public string Role { get; set; } = string.Empty;

        [Required, MinLength(1)]
        public string Content { get; set; } = string.Empty;
    }
}

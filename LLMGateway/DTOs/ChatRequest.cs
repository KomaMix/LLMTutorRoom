using System.ComponentModel.DataAnnotations;

namespace LLMGateway.DTOs
{
    public class ChatRequest
    {
        [Required, MaxLength(200)]
        public string Model { get; set; } = string.Empty;

        [Required, MinLength(1)]
        public IReadOnlyCollection<ChatMessageRequest> Messages { get; set; } = Array.Empty<ChatMessageRequest>();

        [Range(0, 2)]
        public float? Temperature { get; set; }
    }
}

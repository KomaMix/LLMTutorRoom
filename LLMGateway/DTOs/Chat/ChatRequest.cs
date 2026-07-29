using System.ComponentModel.DataAnnotations;

namespace LLMGateway.DTOs.Chat
{
    public class ChatRequest
    {
        [Required, MinLength(1)]
        public IReadOnlyCollection<ChatMessageRequest> Messages { get; set; } = Array.Empty<ChatMessageRequest>();

        [Range(0, 2)]
        public float? Temperature { get; set; }
    }
}

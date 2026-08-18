using System.ComponentModel.DataAnnotations;

namespace LLMGateway.DTOs.Chat
{
    public class ChatRequest
    {
        [Required, MinLength(1)]
        public List<ChatMessageRequest> Messages { get; set; } = new();

        [Range(0, 2)]
        public float? Temperature { get; set; }
    }
}

namespace LLMGateway.DTOs
{
    public class ChatResponse
    {
        public string Model { get; set; } = string.Empty;
        public int DeploymentId { get; set; }
        public string ProviderType { get; set; } = string.Empty;
        public string ProviderModelId { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }
}

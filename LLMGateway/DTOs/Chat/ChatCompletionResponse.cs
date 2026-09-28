namespace LLMGateway.DTOs.Chat;

public sealed record ChatCompletionResponse
{
    public int ModelId { get; init; }
    public string ModelDisplayName { get; init; } = string.Empty;
    public int DeploymentId { get; init; }
    public string DeploymentName { get; init; } = string.Empty;
    public string ProviderModelId { get; init; } = string.Empty;
    public DateTimeOffset DeploymentUpdatedAt { get; init; }
    public string Text { get; init; } = string.Empty;
}

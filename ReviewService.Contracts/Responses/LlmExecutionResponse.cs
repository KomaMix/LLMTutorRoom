namespace ReviewService.Contracts.Responses;

public sealed record LlmExecutionResponse(
    int ModelId, string ModelDisplayName, int DeploymentId, string DeploymentName,
    string ProviderModelId, DateTimeOffset DeploymentUpdatedAt);

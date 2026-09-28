namespace LLMGateway.DTOs.Models;

public sealed record DeploymentCheckResponse(bool IsAvailable, string Message, DateTimeOffset CheckedAt, long ElapsedMilliseconds);

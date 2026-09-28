namespace LLMGateway.DTOs.Models;

public sealed record RateLimitRuleResponse(int Id, int DeploymentId, int WindowSeconds, int MaxRequests);

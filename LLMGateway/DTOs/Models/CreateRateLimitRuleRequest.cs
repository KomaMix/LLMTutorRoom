using System.ComponentModel.DataAnnotations;

namespace LLMGateway.DTOs.Models;

public sealed record CreateRateLimitRuleRequest
{
    [Range(1, 86400)]
    public int WindowSeconds { get; init; }
    [Range(1, 1000000)]
    public int MaxRequests { get; init; }
}

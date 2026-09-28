using System.ComponentModel.DataAnnotations;
using LLMGateway.Validation;

namespace LLMGateway.DTOs.Models;

public sealed record UpdateModelDeploymentRequest
{
    [Required, MaxLength(200)]
    public string Name { get; init; } = string.Empty;
    [Required, MaxLength(2000), HttpEndpoint]
    public string Endpoint { get; init; } = string.Empty;
    [MaxLength(4096)]
    public string? ApiKey { get; init; }
    public bool ClearApiKey { get; init; }
    [Required, MaxLength(300)]
    public string ProviderModelId { get; init; } = string.Empty;
    public bool IsEnabled { get; init; } = true;
    [Range(0, int.MaxValue)]
    public int Priority { get; init; }
    [Range(1, int.MaxValue)]
    public int? MaxConcurrentRequests { get; init; }
}

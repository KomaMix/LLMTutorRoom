using System.ComponentModel.DataAnnotations;

namespace LLMGateway.DTOs.Models;

public sealed record UpdateModelRequest
{
    [Required, MaxLength(200)]
    public string Key { get; init; } = string.Empty;
    [Required, MaxLength(200)]
    public string DisplayName { get; init; } = string.Empty;
    [MaxLength(2000)]
    public string? Description { get; init; }
    public bool IsArchived { get; init; }
}

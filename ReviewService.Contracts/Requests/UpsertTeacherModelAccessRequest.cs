using System.ComponentModel.DataAnnotations;

namespace ReviewService.Contracts.Requests;

public sealed record UpsertTeacherModelAccessRequest
{
    public required bool IsEnabled { get; init; }

    [Range(1, int.MaxValue)]
    public required int PeriodSeconds { get; init; }

    [Range(0, int.MaxValue)]
    public required int MaxChecks { get; init; }
}

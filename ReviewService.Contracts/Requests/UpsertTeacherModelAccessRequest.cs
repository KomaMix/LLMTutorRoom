using System.ComponentModel.DataAnnotations;

namespace ReviewService.Contracts.Requests;

public sealed class UpsertTeacherModelAccessRequest
{
    public required bool IsEnabled { get; set; }

    [Range(1, int.MaxValue)]
    public required int PeriodSeconds { get; set; }

    [Range(1, int.MaxValue)]
    public required int MaxChecks { get; set; }
}

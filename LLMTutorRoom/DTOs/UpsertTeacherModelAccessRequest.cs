using System.ComponentModel.DataAnnotations;

namespace LLMTutorRoom.DTOs
{
    public sealed record UpsertTeacherModelAccessRequest
    {
        public bool IsEnabled { get; init; } = true;

        [Range(1, int.MaxValue)]
        public int PeriodSeconds { get; init; } = 30 * 24 * 60 * 60;

        [Range(1, int.MaxValue)]
        public int MaxChecks { get; init; } = 100;
    }
}

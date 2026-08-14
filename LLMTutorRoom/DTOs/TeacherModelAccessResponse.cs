namespace LLMTutorRoom.DTOs
{
    public sealed record TeacherModelAccessResponse(
        string TeacherUserId,
        string ModelKey,
        string DisplayName,
        bool IsEnabled,
        bool HasEnabledDeployment,
        int PeriodSeconds,
        int MaxChecks,
        int UsedChecks,
        int RemainingChecks,
        DateTimeOffset PeriodStartedAt,
        DateTimeOffset PeriodEndsAt);
}

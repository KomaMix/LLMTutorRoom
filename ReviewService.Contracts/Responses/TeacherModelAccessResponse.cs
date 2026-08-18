namespace ReviewService.Contracts.Responses;

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

using ReviewService.Enums;

namespace ReviewService.Models.ModelAccess;

public sealed record ModelQuotaConsumptionResult(
    ModelQuotaConsumptionStatus Status,
    int RequestedChecks,
    int UsedChecks,
    int RemainingChecks,
    DateTimeOffset PeriodEndsAt);

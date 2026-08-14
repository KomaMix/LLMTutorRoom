namespace LLMTutorRoom.Services.ReviewProcessing
{
    public sealed record ModelQuotaConsumptionResult(
        ModelQuotaConsumptionStatus Status,
        int RequestedChecks,
        int UsedChecks,
        int RemainingChecks,
        DateTimeOffset PeriodEndsAt)
    {
        public static ModelQuotaConsumptionResult GatewayUnavailable(int requestedChecks)
        {
            return new ModelQuotaConsumptionResult(
                ModelQuotaConsumptionStatus.GatewayUnavailable,
                requestedChecks,
                0,
                0,
                DateTimeOffset.UtcNow);
        }
    }
}

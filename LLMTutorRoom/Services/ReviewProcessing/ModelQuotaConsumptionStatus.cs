namespace LLMTutorRoom.Services.ReviewProcessing
{
    public enum ModelQuotaConsumptionStatus
    {
        Allowed,
        AccessNotFound,
        LimitExceeded,
        InvalidCheckCount,
        GatewayUnavailable
    }
}

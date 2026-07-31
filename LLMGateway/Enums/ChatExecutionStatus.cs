namespace LLMGateway.Enums
{
    public enum ChatExecutionStatus
    {
        Completed,
        ModelNotFound,
        NoAvailableDeployment,
        RateLimitExceeded,
        ConcurrencyLimitExceeded,
        ProviderUnavailable,
        ProviderFailed,
        ProviderTimedOut
    }
}

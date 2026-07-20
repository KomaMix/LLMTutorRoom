namespace LLMGateway.Enums
{
    public enum ChatExecutionStatus
    {
        Completed,
        ModelNotFound,
        NoAvailableDeployment,
        RateLimitExceeded,
        ConcurrencyLimitExceeded,
        ProviderFailed,
        ProviderTimedOut
    }
}

namespace LLMGateway.Enums
{
    public enum ChatExecutionStatus
    {
        Completed,
        ModelNotFound,
        NoAvailableDeployment,
        RateLimitExceeded,
        ProviderFailed,
        ProviderTimedOut
    }
}

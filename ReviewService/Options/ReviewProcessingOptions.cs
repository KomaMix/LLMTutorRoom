namespace ReviewService.Options;

public sealed class ReviewProcessingOptions
{
    public const string SectionName = "ReviewProcessing";

    public bool WorkerEnabled { get; set; } = true;
    public bool LlmGatewayEnabled { get; set; } = true;
    public string LlmGatewayBaseUrl { get; set; } = "http://localhost:5200";
    public int LlmRequestTimeoutSeconds { get; set; } = 90;
    public int ProcessingLeaseSeconds { get; set; } = 300;
    public int QueueMaintenanceIntervalSeconds { get; set; } = 30;
    public int EnqueueThrottleSeconds { get; set; } = 120;
}

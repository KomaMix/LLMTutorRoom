namespace LLMTutorRoom.Services.ReviewProcessing
{
    public sealed class ReviewProcessingOptions
    {
        public bool WorkerEnabled { get; set; } = true;
        public bool LlmGatewayEnabled { get; set; }
        public string LlmGatewayBaseUrl { get; set; } = "http://localhost:5200";
        public string LlmModelKey { get; set; } = "gemma3:12b";
        public int LlmRequestTimeoutSeconds { get; set; } = 90;
        public int ProcessingLeaseSeconds { get; set; } = 300;
        public int QueueMaintenanceIntervalSeconds { get; set; } = 30;
        public int EnqueueThrottleSeconds { get; set; } = 120;
        public int TestLlmFailurePauseSeconds { get; set; } = 1800;
    }
}

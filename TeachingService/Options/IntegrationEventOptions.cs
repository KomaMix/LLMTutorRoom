namespace TeachingService.Options
{
    public sealed class IntegrationEventOptions
    {
        public const string SectionName = "IntegrationEvents";

        public bool Enabled { get; set; } = true;
        public string HostName { get; set; } = "localhost";
        public int Port { get; set; } = 5672;
        public string VirtualHost { get; set; } = "/";
        public string UserName { get; set; } = "llm";
        public string Password { get; set; } = "llm-dev";
        public int PollIntervalMilliseconds { get; set; } = 1000;
        public int BatchSize { get; set; } = 20;
        public int RetryBaseDelaySeconds { get; set; } = 5;
        public int RetryMaxDelaySeconds { get; set; } = 300;
        public int PublishedMessageRetentionDays { get; set; } = 30;
        public int CleanupIntervalMinutes { get; set; } = 60;
        public int CleanupBatchSize { get; set; } = 500;
    }
}

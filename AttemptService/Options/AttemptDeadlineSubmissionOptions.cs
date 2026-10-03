namespace AttemptService.Options;

public class AttemptDeadlineSubmissionOptions
{
    public const string SectionName = "AttemptDeadlineSubmission";

    public bool Enabled { get; set; } = true;
    public int PollIntervalSeconds { get; set; } = 5;
    public int BatchSize { get; set; } = 200;
}

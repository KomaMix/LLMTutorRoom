namespace AttemptService.Options;

public sealed class AttemptInputLimitsOptions
{
    public const string SectionName = "AttemptInputLimits";

    public long MaxRequestBodyBytes { get; set; } = 262_144;
    public int MaxAnswerCount { get; set; } = 200;
    public int MaxAnswerLength { get; set; } = 10_000;
    public int MaxTotalAnswerLength { get; set; } = 100_000;
}

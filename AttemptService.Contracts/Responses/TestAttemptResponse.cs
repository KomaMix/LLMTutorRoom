using AttemptService.Contracts.Enums;

namespace AttemptService.Contracts.Responses;

public sealed class TestAttemptResponse
{
    public Guid Id { get; set; }
    public string TestId { get; set; } = string.Empty;
    public int TestRevision { get; set; }
    public TestAttemptStatus Status { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public Dictionary<string, string> Answers { get; set; } = new();
}

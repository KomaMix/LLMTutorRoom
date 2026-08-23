using AttemptService.Contracts.Enums;

namespace AttemptService.Models;

public sealed class TestAttempt
{
    public Guid Id { get; set; }
    public string TestId { get; set; } = string.Empty;
    public int TestRevision { get; set; }
    public string StudentUserId { get; set; } = string.Empty;
    public TestAttemptStatus Status { get; set; } = TestAttemptStatus.InProgress;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public string AnswersJson { get; set; } = "{}";
    public string AllowedTaskIdsJson { get; set; } = "[]";
    public int StateRevision { get; set; }
}

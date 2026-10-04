using AttemptService.Contracts.Enums;

namespace AttemptService.Models;

public class TestAttempt
{
    public Guid Id { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid GroupId { get; set; }
    public string TestId { get; set; } = string.Empty;
    public int TestRevision { get; set; }
    public string TeacherUserId { get; set; } = string.Empty;
    public string StudentUserId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public TestAttemptStatus Status { get; set; } = TestAttemptStatus.InProgress;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public AttemptSubmissionReason? SubmissionReason { get; set; }
    public string AnswersJson { get; set; } = "{}";
    public string AllowedTaskIdsJson { get; set; } = "[]";
    public int StateRevision { get; set; }
}

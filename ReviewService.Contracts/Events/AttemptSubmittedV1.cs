namespace ReviewService.Contracts.Events;

public sealed record AttemptSubmittedV1(
    Guid EventId,
    int AttemptId,
    string TestId,
    int TestRevision,
    string StudentUserId,
    string StudentName,
    Dictionary<string, string> Answers,
    DateTimeOffset SubmittedAt);

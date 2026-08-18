namespace ReviewService.Contracts.Events;

public sealed record TestReviewPolicyPublishedV1(
    Guid EventId,
    string TestId,
    int Revision,
    string TeacherUserId,
    string TestTitle,
    string ModelKey,
    List<ReviewTaskPolicySnapshot> Tasks,
    DateTimeOffset PublishedAt);

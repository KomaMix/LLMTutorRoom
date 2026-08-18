namespace ReviewService.Contracts.Events;

public sealed record ReviewAnswerOptionSnapshot(
    string Id,
    string Text,
    bool IsCorrect);

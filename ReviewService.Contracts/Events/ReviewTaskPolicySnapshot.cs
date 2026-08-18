using ReviewService.Contracts.Enums;

namespace ReviewService.Contracts.Events;

public sealed record ReviewTaskPolicySnapshot(
    string Id,
    ReviewTaskType Type,
    ReviewCheckMode CheckMode,
    string Title,
    string Prompt,
    decimal MaxPoints,
    decimal WrongAnswerPenalty,
    List<ReviewAnswerOptionSnapshot> Options);

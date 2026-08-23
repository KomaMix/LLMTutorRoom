using ReviewService.Contracts.Enums;

namespace ReviewService.Contracts.Events;

public sealed class ReviewTaskPolicySnapshot
{
    public string Id { get; set; } = string.Empty;
    public ReviewTaskType Type { get; set; }
    public ReviewCheckMode CheckMode { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public decimal MaxPoints { get; set; }
    public decimal WrongAnswerPenalty { get; set; }
    public List<ReviewAnswerOptionSnapshot> Options { get; set; } = new();
}

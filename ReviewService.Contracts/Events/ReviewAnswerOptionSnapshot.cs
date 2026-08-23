namespace ReviewService.Contracts.Events;

public sealed class ReviewAnswerOptionSnapshot
{
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
}

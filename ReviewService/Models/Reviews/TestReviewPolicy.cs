namespace ReviewService.Models.Reviews;

public sealed class TestReviewPolicy
{
    public int Id { get; set; }
    public string TestId { get; set; } = string.Empty;
    public int Revision { get; set; }
    public string TeacherUserId { get; set; } = string.Empty;
    public string TestTitle { get; set; } = string.Empty;
    public int? ModelId { get; set; }
    public string FeedbackTemplateJson { get; set; } = "{}";
    public string TasksJson { get; set; } = "[]";
    public string GradingExamplesJson { get; set; } = "[]";
    public DateTimeOffset PublishedAt { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
}

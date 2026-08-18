namespace ReviewService.Models.ModelAccess;

public sealed class TeacherModelUsage
{
    public int Id { get; set; }
    public string TeacherUserId { get; set; } = string.Empty;
    public string ModelKey { get; set; } = string.Empty;
    public DateTimeOffset PeriodStart { get; set; }
    public int PeriodSeconds { get; set; }
    public int UsedChecks { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

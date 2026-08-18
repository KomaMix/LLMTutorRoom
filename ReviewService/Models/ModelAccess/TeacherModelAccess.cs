namespace ReviewService.Models.ModelAccess;

public sealed class TeacherModelAccess
{
    public int Id { get; set; }
    public string TeacherUserId { get; set; } = string.Empty;
    public string ModelKey { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public int PeriodSeconds { get; set; } = 30 * 24 * 60 * 60;
    public int MaxChecks { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

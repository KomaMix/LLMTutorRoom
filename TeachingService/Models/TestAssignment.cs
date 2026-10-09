namespace TeachingService.Models;

public class TestAssignment
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public Guid CourseTestVersionId { get; set; }
    public CourseTestVersion CourseTestVersion { get; set; } = null!;
    public DateTimeOffset AvailableFrom { get; set; }
    public DateTimeOffset Deadline { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

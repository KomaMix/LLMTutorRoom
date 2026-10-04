namespace TeachingService.Models;

public class TestAssignment
{
    public Guid Id { get; set; }
    public Guid StudyGroupId { get; set; }
    public StudyGroup StudyGroup { get; set; } = null!;
    public Guid CourseTestVersionId { get; set; }
    public CourseTestVersion CourseTestVersion { get; set; } = null!;
    public DateTimeOffset AvailableFrom { get; set; }
    public DateTimeOffset Deadline { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

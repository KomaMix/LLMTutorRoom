using TeachingService.Contracts.Models;

namespace TeachingService.Contracts.Responses;

public class TestAssignmentResponse
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string TeacherUserId { get; set; } = string.Empty;
    public DateTimeOffset AvailableFrom { get; set; }
    public DateTimeOffset Deadline { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public CourseTestDto Test { get; set; } = new();
}

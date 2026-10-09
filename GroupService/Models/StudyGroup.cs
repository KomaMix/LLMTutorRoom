namespace GroupService.Models;

public class StudyGroup
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TeacherUserId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int Revision { get; set; }
    public List<StudyGroupStudent> Students { get; set; } = new();
}

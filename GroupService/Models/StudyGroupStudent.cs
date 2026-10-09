namespace GroupService.Models;

public class StudyGroupStudent
{
    public Guid StudyGroupId { get; set; }
    public StudyGroup StudyGroup { get; set; } = null!;
    public string StudentUserId { get; set; } = string.Empty;
    public DateTimeOffset AddedAt { get; set; }
}

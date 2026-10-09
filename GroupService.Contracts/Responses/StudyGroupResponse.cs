namespace GroupService.Contracts.Responses;

public class StudyGroupResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TeacherUserId { get; set; } = string.Empty;
    public IList<string> StudentUserIds { get; set; } = new List<string>();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int Revision { get; set; }
}

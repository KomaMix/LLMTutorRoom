namespace TeachingService.Contracts.Requests;

public class UpdateStudyGroupRequest
{
    public string Name { get; set; } = string.Empty;
    public IList<string> StudentUserIds { get; set; } = new List<string>();
    public int Revision { get; set; }
}

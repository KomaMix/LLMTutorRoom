using TeachingService.Contracts.Enums;

namespace TeachingService.Models;

public class FeedbackTemplate
{
    public Guid Id { get; set; }
    public string TeacherUserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public FeedbackTone Tone { get; set; }
    public FeedbackDetailLevel DetailLevel { get; set; }
    public FeedbackAnswerDisclosure AnswerDisclosure { get; set; }
    public string CustomInstruction { get; set; } = string.Empty;
    public string SectionsJson { get; set; } = "[]";
    public int Revision { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

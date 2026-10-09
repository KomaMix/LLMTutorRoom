using TeachingService.Contracts.Enums;

namespace TeachingService.Contracts.Models;

public class FeedbackTemplateDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
    public FeedbackTone Tone { get; set; }
    public FeedbackDetailLevel DetailLevel { get; set; }
    public FeedbackAnswerDisclosure AnswerDisclosure { get; set; }
    public string CustomInstruction { get; set; } = string.Empty;
    public List<FeedbackTemplateSectionDto> Sections { get; set; } = new();
    public int Revision { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

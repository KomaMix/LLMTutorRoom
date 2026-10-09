using TeachingService.Contracts.Enums;

namespace TeachingService.Contracts.Models;

public class FeedbackTemplateSectionDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Instruction { get; set; } = string.Empty;
    public FeedbackSectionFormat Format { get; set; }
    public int MaxItems { get; set; } = 3;
    public bool IsRequired { get; set; } = true;
}

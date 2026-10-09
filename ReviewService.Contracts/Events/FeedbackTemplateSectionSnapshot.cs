using ReviewService.Contracts.Enums;

namespace ReviewService.Contracts.Events;

public class FeedbackTemplateSectionSnapshot
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Instruction { get; set; } = string.Empty;
    public FeedbackSectionFormat Format { get; set; }
    public int MaxItems { get; set; }
    public bool IsRequired { get; set; }
}

using ReviewService.Contracts.Enums;

namespace ReviewService.Contracts.Events;

public class FeedbackTemplateSnapshot
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public FeedbackTone Tone { get; set; }
    public FeedbackDetailLevel DetailLevel { get; set; }
    public FeedbackAnswerDisclosure AnswerDisclosure { get; set; }
    public string CustomInstruction { get; set; } = string.Empty;
    public List<FeedbackTemplateSectionSnapshot> Sections { get; set; } = new();
}

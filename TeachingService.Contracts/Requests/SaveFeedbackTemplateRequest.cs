using TeachingService.Contracts.Enums;
using TeachingService.Contracts.Models;

namespace TeachingService.Contracts.Requests;

public class SaveFeedbackTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public FeedbackTone Tone { get; set; } = FeedbackTone.Supportive;
    public FeedbackDetailLevel DetailLevel { get; set; } = FeedbackDetailLevel.Standard;
    public FeedbackAnswerDisclosure AnswerDisclosure { get; set; } = FeedbackAnswerDisclosure.ExplainApproach;
    public string CustomInstruction { get; set; } = string.Empty;
    public List<FeedbackTemplateSectionDto> Sections { get; set; } = new();
    public int Revision { get; set; }
}

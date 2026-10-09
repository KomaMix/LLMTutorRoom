using ReviewService.Contracts.Enums;

namespace ReviewService.Contracts.Responses;

public class FeedbackSectionResponse
{
    public Guid SectionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public FeedbackSectionFormat Format { get; set; }
    public string Text { get; set; } = string.Empty;
    public List<string> Items { get; set; } = new();
}

using System.ComponentModel.DataAnnotations;
using TeachingService.Contracts.Enums;
using TeachingService.Contracts.Models;

namespace TeachingService.Contracts.Requests;

public class CreateTestRequest
{
    public string Title { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public CourseTestStatus Status { get; set; } = CourseTestStatus.Draft;
    public int TimeLimitMinutes { get; set; } = 45;
    [Range(1, int.MaxValue)]
    public int? LlmModelId { get; set; }
    public Guid? FeedbackTemplateId { get; set; }
    public List<GradingExampleDto> GradingExamples { get; set; } = new();
}

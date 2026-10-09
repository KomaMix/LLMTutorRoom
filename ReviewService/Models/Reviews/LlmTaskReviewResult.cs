using ReviewService.Contracts.Responses;

namespace ReviewService.Models.Reviews;

public class LlmTaskReviewResult
{
    public decimal Score { get; set; }
    public List<LlmFeedbackSectionResult> Sections { get; set; } = new();
    public LlmExecutionResponse? Execution { get; set; }
}

using ReviewService.Contracts.Responses;

namespace ReviewService.Models.Reviews;

public sealed record LlmTaskReviewResult(
    decimal Score,
    string Feedback,
    List<string> Findings)
{
    public LlmExecutionResponse? Execution { get; init; }
}

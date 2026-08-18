using System.ComponentModel.DataAnnotations;

namespace ReviewService.Contracts.Requests;

public sealed record UpdateManualTaskReviewRequest
{
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public required decimal Score { get; init; }

    public string Feedback { get; init; } = string.Empty;

    public List<string> Findings { get; init; } = [];
}

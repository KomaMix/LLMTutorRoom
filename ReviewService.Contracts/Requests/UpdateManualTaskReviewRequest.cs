using System.ComponentModel.DataAnnotations;

namespace ReviewService.Contracts.Requests;

public sealed class UpdateManualTaskReviewRequest
{
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public required decimal Score { get; set; }

    public string Feedback { get; set; } = string.Empty;

    public List<string> Findings { get; set; } = [];
}

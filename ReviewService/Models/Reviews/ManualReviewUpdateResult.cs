using ReviewService.Contracts.Responses;
using ReviewService.Enums;

namespace ReviewService.Models.Reviews;

public sealed record ManualReviewUpdateResult(
    ManualReviewUpdateStatus Status,
    ReviewResponse? Review = null);

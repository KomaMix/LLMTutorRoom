using ReviewService.Contracts.Responses;
using ReviewService.Enums;

namespace ReviewService.Models.Reviews;

public sealed record ReviewPageQueryResult(
    ReviewPageQueryStatus Status,
    ReviewPageResponse? Page = null);

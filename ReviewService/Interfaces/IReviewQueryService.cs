using ReviewService.Contracts.Requests;
using ReviewService.Models.Reviews;

namespace ReviewService.Interfaces;

public interface IReviewQueryService
{
    Task<ReviewPageQueryResult> GetTeacherReviewsAsync(
        string teacherUserId,
        bool includeNonTerminal,
        bool includeHistoricalVersions,
        int pageSize,
        string? cursor,
        CancellationToken cancellationToken);

    Task<ReviewPageQueryResult> GetStudentReviewsAsync(
        string studentUserId,
        bool includeNonTerminal,
        int pageSize,
        string? cursor,
        CancellationToken cancellationToken);

    Task<ManualReviewUpdateResult> UpdateManualTaskReviewAsync(
        int reviewId,
        string taskId,
        string teacherUserId,
        UpdateManualTaskReviewRequest request,
        CancellationToken cancellationToken);
}

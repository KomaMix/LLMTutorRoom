using LLMTutorRoom.Services.Reviews;
using ReviewService.Contracts.Requests;
using ReviewService.Contracts.Responses;

namespace LLMTutorRoom.Interfaces;

public interface IReviewServiceClient
{
    Task<ReviewPageResponse> GetTeacherReviewsAsync(
        string teacherUserId,
        CancellationToken cancellationToken);

    Task<ReviewPageResponse> GetStudentReviewsAsync(
        string studentUserId,
        CancellationToken cancellationToken);

    Task<ReviewServiceResult<ReviewPageResponse>> GetTeacherReviewHistoryAsync(
        string teacherUserId,
        bool includeHistoricalVersions,
        int pageSize,
        string? cursor,
        CancellationToken cancellationToken);

    Task<ReviewServiceResult<ReviewPageResponse>> GetStudentReviewHistoryAsync(
        string studentUserId,
        int pageSize,
        string? cursor,
        CancellationToken cancellationToken);

    Task<ReviewServiceResult<ReviewResponse>> UpdateManualTaskReviewAsync(
        int reviewId,
        string taskId,
        string teacherUserId,
        UpdateManualTaskReviewRequest request,
        CancellationToken cancellationToken);

    Task<List<LlmModelCatalogItemResponse>> GetModelCatalogAsync(
        CancellationToken cancellationToken);

    Task<List<TeacherModelAccessResponse>> GetTeacherModelAccessAsync(
        string teacherUserId,
        bool includeDisabled,
        CancellationToken cancellationToken);

    Task<ReviewServiceResult<TeacherModelAccessResponse>> UpsertTeacherModelAccessAsync(
        string teacherUserId,
        string modelKey,
        UpsertTeacherModelAccessRequest request,
        CancellationToken cancellationToken);

    Task<ReviewServiceResult<object>> DeleteTeacherModelAccessAsync(
        string teacherUserId,
        string modelKey,
        CancellationToken cancellationToken);
}

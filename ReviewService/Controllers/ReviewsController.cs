using Microsoft.AspNetCore.Mvc;
using ReviewService.Contracts.Requests;
using ReviewService.Contracts.Responses;
using ReviewService.Enums;
using ReviewService.Interfaces;
using ReviewService.Models.Reviews;

namespace ReviewService.Controllers;

[ApiController]
[Route("internal/reviews")]
public sealed class ReviewsController(IReviewQueryService reviewService) : ControllerBase
{
    [HttpGet("teachers/{teacherUserId}")]
    public Task<ActionResult<ReviewPageResponse>> GetTeacherReviews(
        [FromRoute] string teacherUserId,
        CancellationToken cancellationToken,
        [FromQuery] int pageSize = ReviewPagination.DefaultPageSize,
        [FromQuery] string? cursor = null)
    {
        return GetTeacherReviewsCore(
            teacherUserId,
            includeNonTerminal: true,
            includeHistoricalVersions: false,
            pageSize,
            cursor,
            cancellationToken);
    }

    [HttpGet("teachers/{teacherUserId}/history")]
    public Task<ActionResult<ReviewPageResponse>> GetTeacherReviewHistory(
        [FromRoute] string teacherUserId,
        CancellationToken cancellationToken,
        [FromQuery] bool includeHistoricalVersions = true,
        [FromQuery] int pageSize = ReviewPagination.DefaultPageSize,
        [FromQuery] string? cursor = null)
    {
        return GetTeacherReviewsCore(
            teacherUserId,
            includeNonTerminal: false,
            includeHistoricalVersions,
            pageSize,
            cursor,
            cancellationToken);
    }

    [HttpGet("students/{studentUserId}")]
    public Task<ActionResult<ReviewPageResponse>> GetStudentReviews(
        [FromRoute] string studentUserId,
        CancellationToken cancellationToken,
        [FromQuery] int pageSize = ReviewPagination.DefaultPageSize,
        [FromQuery] string? cursor = null)
    {
        return GetStudentReviewsCore(
            studentUserId,
            includeNonTerminal: true,
            pageSize,
            cursor,
            cancellationToken);
    }

    [HttpGet("students/{studentUserId}/history")]
    public Task<ActionResult<ReviewPageResponse>> GetStudentReviewHistory(
        [FromRoute] string studentUserId,
        CancellationToken cancellationToken,
        [FromQuery] int pageSize = ReviewPagination.DefaultPageSize,
        [FromQuery] string? cursor = null)
    {
        return GetStudentReviewsCore(
            studentUserId,
            includeNonTerminal: false,
            pageSize,
            cursor,
            cancellationToken);
    }

    [HttpPut("{reviewId:int}/tasks/{taskId}/manual")]
    public async Task<ActionResult<ReviewResponse>> UpdateManualTaskReview(
        [FromRoute] int reviewId,
        [FromRoute] string taskId,
        [FromQuery] string teacherUserId,
        [FromBody] UpdateManualTaskReviewRequest request,
        CancellationToken cancellationToken)
    {
        var result = await reviewService.UpdateManualTaskReviewAsync(
            reviewId,
            taskId,
            teacherUserId,
            request,
            cancellationToken);

        return result.Status switch
        {
            ManualReviewUpdateStatus.Updated => Ok(result.Review),
            ManualReviewUpdateStatus.NotFound => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Review task not found."),
            ManualReviewUpdateStatus.Forbidden => Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "The teacher does not own this review."),
            ManualReviewUpdateStatus.InvalidState => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The task is not waiting for manual review."),
            ManualReviewUpdateStatus.InvalidScore => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Score must be between zero and the task maximum."),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    private async Task<ActionResult<ReviewPageResponse>> GetTeacherReviewsCore(
        string teacherUserId,
        bool includeNonTerminal,
        bool includeHistoricalVersions,
        int pageSize,
        string? cursor,
        CancellationToken cancellationToken)
    {
        if (pageSize <= 0)
            return InvalidPageSize();

        var result = await reviewService.GetTeacherReviewsAsync(
            teacherUserId,
            includeNonTerminal,
            includeHistoricalVersions,
            pageSize,
            cursor,
            cancellationToken);

        return ToPageActionResult(result);
    }

    private async Task<ActionResult<ReviewPageResponse>> GetStudentReviewsCore(
        string studentUserId,
        bool includeNonTerminal,
        int pageSize,
        string? cursor,
        CancellationToken cancellationToken)
    {
        if (pageSize <= 0)
            return InvalidPageSize();

        var result = await reviewService.GetStudentReviewsAsync(
            studentUserId,
            includeNonTerminal,
            pageSize,
            cursor,
            cancellationToken);

        return ToPageActionResult(result);
    }

    private ActionResult<ReviewPageResponse> ToPageActionResult(ReviewPageQueryResult result)
    {
        return result.Status switch
        {
            ReviewPageQueryStatus.Success when result.Page is not null => Ok(result.Page),
            ReviewPageQueryStatus.InvalidPageSize => InvalidPageSize(),
            ReviewPageQueryStatus.InvalidCursor => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid review cursor.",
                detail: "Use the opaque nextCursor returned by the previous page."),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    private ActionResult<ReviewPageResponse> InvalidPageSize()
    {
        return Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid review page size.",
            detail: "pageSize must be greater than zero.");
    }
}

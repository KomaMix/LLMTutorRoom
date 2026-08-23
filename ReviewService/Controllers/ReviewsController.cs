using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReviewService.Contracts.Requests;
using ReviewService.Contracts.Responses;
using ReviewService.Enums;
using ReviewService.Interfaces;

namespace ReviewService.Controllers;

[ApiController]
[Authorize(Roles = "Teacher")]
[Route("api/reviews")]
public sealed class ReviewsController(IReviewQueryService reviewService) : ControllerBase
{
    [HttpPut("{reviewId:int}/tasks/{taskId}/manual")]
    public async Task<ActionResult<ReviewResponse>> UpdateManualTaskReview(
        [FromRoute] int reviewId,
        [FromRoute] string taskId,
        [FromBody] UpdateManualTaskReviewRequest request,
        CancellationToken cancellationToken)
    {
        var teacherUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(teacherUserId))
            return Unauthorized();

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
}

using LLMTutorRoom.DTOs;
using LLMTutorRoom.Models;
using LLMTutorRoom.Services;
using LLMTutorRoom.Services.Reviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReviewService.Contracts.Responses;
using System.Security.Claims;
using System.Text.Json;

namespace LLMTutorRoom.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/classroom")]
    public class ClassroomController : ControllerBase
    {
        private readonly ClassroomService _classroomService;

        public ClassroomController(ClassroomService classroomService)
        {
            _classroomService = classroomService;
        }

        [HttpGet("overview")]
        public async Task<ActionResult<ClassroomOverview>> GetOverview(CancellationToken cancellationToken)
        {
            if (User.IsInRole("Teacher"))
                return Ok(await _classroomService.GetTeacherOverviewAsync(
                    GetUserId(),
                    cancellationToken));

            if (User.IsInRole("Student"))
                return Ok(await _classroomService.GetStudentOverviewAsync(
                    GetUserId(),
                    cancellationToken));

            return Forbid();
        }

        [HttpGet("reviews/history")]
        public async Task<ActionResult<ReviewHistoryPageResponse>> GetReviewHistory(
            CancellationToken cancellationToken,
            [FromQuery] bool includeHistoricalVersions = true,
            [FromQuery] int pageSize = ReviewPagination.DefaultPageSize,
            [FromQuery] string? cursor = null)
        {
            if (!User.IsInRole("Teacher") && !User.IsInRole("Student"))
                return Forbid();

            if (pageSize <= 0)
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid review page size.",
                    detail: "pageSize must be greater than zero.");
            }

            var result = await _classroomService.GetReviewHistoryAsync(
                GetUserId(),
                User.IsInRole("Teacher"),
                includeHistoricalVersions,
                Math.Min(pageSize, ReviewPagination.MaximumPageSize),
                cursor,
                cancellationToken);

            if (result.IsSuccess)
            {
                return result.Value is null
                    ? StatusCode(StatusCodes.Status502BadGateway)
                    : Ok(result.Value);
            }

            return ToReviewHistoryProblem(result);
        }

        [Authorize(Roles = "Teacher")]
        [HttpPut("reviews/{reviewId:int}/tasks/{taskId}/manual")]
        public async Task<ActionResult<ReviewResponse>> UpdateManualTaskReview(
            int reviewId,
            string taskId,
            [FromBody] ManualTaskReviewRequest request,
            CancellationToken cancellationToken)
        {
            if (request.Score < 0)
                return BadRequest("Score must not be negative.");

            var review = await _classroomService.UpdateManualTaskReviewAsync(
                reviewId,
                taskId,
                GetUserId(),
                request,
                cancellationToken);

            if (review.IsSuccess)
            {
                return review.Value is null
                    ? StatusCode(StatusCodes.Status502BadGateway)
                    : Ok(review.Value);
            }

            return StatusCode((int)review.StatusCode, review.Error);
        }

        private string GetUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? string.Empty;
        }

        private ActionResult<ReviewHistoryPageResponse> ToReviewHistoryProblem(
            ReviewServiceResult<ReviewHistoryPageResponse> result)
        {
            ProblemDetails? upstreamProblem = null;
            try
            {
                upstreamProblem = JsonSerializer.Deserialize<ProblemDetails>(
                    result.Error,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
            }
            catch (JsonException)
            {
                // The fallback below deliberately avoids exposing an arbitrary upstream body.
            }

            var statusCode = (int)result.StatusCode;
            return Problem(
                statusCode: statusCode,
                title: upstreamProblem?.Title ?? "Review history request failed.",
                detail: upstreamProblem?.Detail);
        }

    }
}

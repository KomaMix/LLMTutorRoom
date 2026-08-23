using LLMTutorRoom.DTOs;
using LLMTutorRoom.Enums;
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

        [Authorize(Roles = "Student")]
        [HttpPost("tests/{testId}/attempts/start")]
        public async Task<ActionResult<TestAttemptResponse>> StartAttempt(
            string testId,
            [FromQuery] int? versionNumber,
            CancellationToken cancellationToken)
        {
            var result = await _classroomService.StartAttemptAsync(
                testId,
                GetUserId(),
                versionNumber,
                cancellationToken);

            return result.Outcome switch
            {
                StartAttemptOutcome.Success when result.Attempt is not null => Ok(result.Attempt),
                StartAttemptOutcome.VersionConflict => Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Test version changed",
                    Detail = "The published test version changed. Refresh the catalog before starting."
                }),
                _ => NotFound()
            };
        }

        [Authorize(Roles = "Student")]
        [HttpPut("attempts/{attemptId:int}/answers")]
        public async Task<ActionResult<TestAttemptResponse>> SaveAttemptAnswers(
            int attemptId,
            [FromBody] SaveAttemptAnswersRequest request,
            CancellationToken cancellationToken)
        {
            TestAttemptResponse? attempt;
            try
            {
                attempt = await _classroomService.SaveAttemptAnswersAsync(
                    attemptId,
                    GetUserId(),
                    request.Answers,
                    cancellationToken);
            }
            catch (AttemptWriteConflictException exception)
            {
                return Conflict(CreateAttemptConflictProblem(exception.Message));
            }

            if (attempt is null)
                return NotFound();

            return attempt.Status == TestAttemptStatus.InProgress
                ? Ok(attempt)
                : Conflict(attempt);
        }

        [Authorize(Roles = "Student")]
        [HttpPost("attempts/{attemptId:int}/submit")]
        public async Task<ActionResult<TestAttemptResponse>> SubmitAttempt(
            int attemptId,
            CancellationToken cancellationToken)
        {
            TestAttemptResponse? attempt;
            try
            {
                attempt = await _classroomService.SubmitAttemptAsync(
                    attemptId,
                    GetUserId(),
                    GetUserName(),
                    cancellationToken);
            }
            catch (AttemptWriteConflictException exception)
            {
                return Conflict(CreateAttemptConflictProblem(exception.Message));
            }

            if (attempt is null)
                return NotFound();

            return attempt.Status == TestAttemptStatus.Submitted
                ? Ok(attempt)
                : Conflict(attempt);
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

        private string GetUserName()
        {
            return User.FindFirstValue(ClaimTypes.Name) ?? "Студент";
        }

        private string GetUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? string.Empty;
        }

        private static ProblemDetails CreateAttemptConflictProblem(string detail)
        {
            return new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Attempt changed concurrently",
                Detail = detail
            };
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

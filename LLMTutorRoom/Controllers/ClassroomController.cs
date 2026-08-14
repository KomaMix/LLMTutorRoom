using LLMTutorRoom.DTOs;
using LLMTutorRoom.Models;
using LLMTutorRoom.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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

        [Authorize(Roles = "Student")]
        [HttpPost("tests/{testId}/attempts/start")]
        public async Task<ActionResult<TestAttemptResponse>> StartAttempt(
            string testId,
            CancellationToken cancellationToken)
        {
            var attempt = await _classroomService.StartAttemptAsync(
                testId,
                GetUserId(),
                cancellationToken);

            return attempt is null
                ? NotFound()
                : Ok(attempt);
        }

        [Authorize(Roles = "Student")]
        [HttpPut("attempts/{attemptId:int}/answers")]
        public async Task<ActionResult<TestAttemptResponse>> SaveAttemptAnswers(
            int attemptId,
            [FromBody] SaveAttemptAnswersRequest request,
            CancellationToken cancellationToken)
        {
            var attempt = await _classroomService.SaveAttemptAnswersAsync(
                attemptId,
                GetUserId(),
                request.Answers,
                cancellationToken);

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
            var attempt = await _classroomService.SubmitAttemptAsync(
                attemptId,
                GetUserId(),
                GetDisplayName(),
                cancellationToken);

            if (attempt is null)
                return NotFound();

            return attempt.Status == TestAttemptStatus.Submitted
                ? Ok(attempt)
                : Conflict(attempt);
        }

        [Authorize(Roles = "Student")]
        [HttpPost("reviews")]
        public async Task<ActionResult<SubmissionReview>> CreateReview(
            [FromBody] ReviewRequest request,
            CancellationToken cancellationToken)
        {
            var review = await _classroomService.CreateReviewAsync(
                request,
                GetUserId(),
                GetDisplayName(),
                cancellationToken);

            return review is null
                ? NotFound()
                : Ok(review);
        }

        [Authorize(Roles = "Teacher")]
        [HttpPut("reviews/{reviewId:int}/tasks/{taskId}/manual")]
        public async Task<ActionResult<SubmissionReview>> UpdateManualTaskReview(
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
                request,
                cancellationToken);

            return review is null
                ? NotFound()
                : Ok(review);
        }

        private string GetDisplayName()
        {
            return User.FindFirstValue(ClaimTypes.Name) ?? "Студент";
        }

        private string GetUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? string.Empty;
        }

    }
}

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
                return Ok(await _classroomService.GetTeacherOverviewAsync(cancellationToken));

            if (User.IsInRole("Student"))
                return Ok(await _classroomService.GetStudentOverviewAsync(
                    GetUserName(),
                    cancellationToken));

            return Forbid();
        }

        [Authorize(Roles = "Teacher")]
        [HttpGet("tests")]
        public async Task<ActionResult<IReadOnlyCollection<CourseTest>>> GetTests(CancellationToken cancellationToken)
        {
            return Ok(await _classroomService.GetTestsAsync(cancellationToken));
        }

        [Authorize(Roles = "Teacher")]
        [HttpPost("tests")]
        public async Task<ActionResult<CourseTest>> CreateTest(
            [FromBody] CreateTestRequest request,
            CancellationToken cancellationToken)
        {
            if (!TryValidateTest(request, out var error))
                return BadRequest(error);

            var test = await _classroomService.CreateTestAsync(request, cancellationToken);
            return Ok(test);
        }

        [Authorize(Roles = "Teacher")]
        [HttpPut("tests/{testId}")]
        public async Task<ActionResult<CourseTest>> UpdateTest(
            string testId,
            [FromBody] CreateTestRequest request,
            CancellationToken cancellationToken)
        {
            if (!TryValidateTest(request, out var error))
                return BadRequest(error);

            var test = await _classroomService.UpdateTestAsync(
                testId,
                request,
                cancellationToken);

            return test is null
                ? NotFound()
                : Ok(test);
        }

        [Authorize(Roles = "Teacher")]
        [HttpPost("tests/{testId}/tasks")]
        public async Task<ActionResult<TestTask>> AddTask(
            string testId,
            [FromBody] CreateTaskRequest request,
            CancellationToken cancellationToken)
        {
            if (!TryValidateTask(request, out var error))
                return BadRequest(error);

            var task = await _classroomService.AddTaskAsync(
                testId,
                request,
                cancellationToken);

            return task is null
                ? NotFound()
                : Ok(task);
        }

        [Authorize(Roles = "Teacher")]
        [HttpPut("tests/{testId}/tasks/{taskId}")]
        public async Task<ActionResult<TestTask>> UpdateTask(
            string testId,
            string taskId,
            [FromBody] CreateTaskRequest request,
            CancellationToken cancellationToken)
        {
            if (!TryValidateTask(request, out var error))
                return BadRequest(error);

            var task = await _classroomService.UpdateTaskAsync(
                testId,
                taskId,
                request,
                cancellationToken);

            return task is null
                ? NotFound()
                : Ok(task);
        }

        [Authorize(Roles = "Teacher")]
        [HttpPatch("tests/{testId}/tasks/{taskId}/visibility")]
        public async Task<ActionResult<TestTask>> SetTaskVisibility(
            string testId,
            string taskId,
            [FromBody] UpdateTaskVisibilityRequest request,
            CancellationToken cancellationToken)
        {
            var task = await _classroomService.SetTaskVisibilityAsync(
                testId,
                taskId,
                request.IsHidden,
                cancellationToken);

            return task is null
                ? NotFound()
                : Ok(task);
        }

        [Authorize(Roles = "Teacher")]
        [HttpDelete("tests/{testId}/tasks/{taskId}")]
        public async Task<IActionResult> DeleteTask(
            string testId,
            string taskId,
            CancellationToken cancellationToken)
        {
            var deleted = await _classroomService.DeleteTaskAsync(
                testId,
                taskId,
                cancellationToken);

            return deleted
                ? NoContent()
                : NotFound();
        }

        [Authorize(Roles = "Student")]
        [HttpPost("tests/{testId}/attempts/start")]
        public async Task<ActionResult<TestAttemptResponse>> StartAttempt(
            string testId,
            CancellationToken cancellationToken)
        {
            var attempt = await _classroomService.StartAttemptAsync(
                testId,
                GetUserName(),
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
                GetUserName(),
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
                GetUserName(),
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
                GetUserName(),
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
            return User.Identity?.Name ?? "Студент";
        }

        private string GetUserName()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? string.Empty;
        }

        private static bool TryValidateTest(
            CreateTestRequest request,
            out string error)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                error = "Title is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.Subject))
            {
                error = "Subject is required.";
                return false;
            }

            if (request.TimeLimitMinutes <= 0)
            {
                error = "TimeLimitMinutes must be greater than zero.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool TryValidateTask(
            CreateTaskRequest request,
            out string error)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                error = "Title is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.Prompt))
            {
                error = "Prompt is required.";
                return false;
            }

            if (request.MaxPoints <= 0)
            {
                error = "MaxPoints must be greater than zero.";
                return false;
            }

            if (request.WrongAnswerPenalty < 0)
            {
                error = "WrongAnswerPenalty must not be negative.";
                return false;
            }

            if (request.Type == TestTaskType.FreeText)
            {
                error = string.Empty;
                return true;
            }

            var options = request.Options ?? new List<string>();
            if (options.Count < 2 || options.Any(string.IsNullOrWhiteSpace))
            {
                error = "Choice tasks must contain at least two non-empty options.";
                return false;
            }

            var correctOptionIndexes = (request.CorrectOptionIndexes ?? new List<int>())
                .Distinct()
                .ToList();

            if (correctOptionIndexes.Any(index => index < 0 || index >= options.Count))
            {
                error = "CorrectOptionIndexes contains invalid option index.";
                return false;
            }

            if (request.Type == TestTaskType.SingleChoice && correctOptionIndexes.Count != 1)
            {
                error = "Single-choice task must contain exactly one correct option.";
                return false;
            }

            if (request.Type == TestTaskType.MultipleChoice && correctOptionIndexes.Count == 0)
            {
                error = "Multiple-choice task must contain at least one correct option.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}

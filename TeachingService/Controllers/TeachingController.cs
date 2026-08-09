using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeachingService.Contracts.Enums;
using TeachingService.Contracts.Models;
using TeachingService.Contracts.Requests;
using TeachingService.Services;

namespace TeachingService.Controllers
{
    [ApiController]
    [Authorize(Roles = "Teacher")]
    [Route("api/teaching")]
    public sealed class TeachingController : ControllerBase
    {
        private readonly TeachingCatalogService _teachingCatalogService;

        public TeachingController(TeachingCatalogService teachingCatalogService)
        {
            _teachingCatalogService = teachingCatalogService;
        }

        [HttpGet("tests")]
        public async Task<ActionResult<List<CourseTestDto>>> GetTests(CancellationToken cancellationToken)
        {
            return Ok(await _teachingCatalogService.GetTestsAsync(
                publishedOnly: false,
                includeHidden: true,
                cancellationToken));
        }

        [HttpGet("tests/{testId}")]
        public async Task<ActionResult<CourseTestDto>> GetTest(
            Guid testId,
            CancellationToken cancellationToken)
        {
            var test = await _teachingCatalogService.GetTestAsync(
                testId,
                includeHidden: true,
                cancellationToken);

            return test is null
                ? NotFound()
                : Ok(test);
        }

        [HttpPost("tests")]
        public async Task<ActionResult<CourseTestDto>> CreateTest(
            [FromBody] CreateTestRequest request,
            CancellationToken cancellationToken)
        {
            if (!TryValidateTest(request, out var error))
                return BadRequest(error);

            var test = await _teachingCatalogService.CreateTestAsync(
                request,
                cancellationToken);

            return Ok(test);
        }

        [HttpPut("tests/{testId}")]
        public async Task<ActionResult<CourseTestDto>> UpdateTest(
            Guid testId,
            [FromBody] CreateTestRequest request,
            CancellationToken cancellationToken)
        {
            if (!TryValidateTest(request, out var error))
                return BadRequest(error);

            var test = await _teachingCatalogService.UpdateTestAsync(
                testId,
                request,
                cancellationToken);

            return test is null
                ? NotFound()
                : Ok(test);
        }

        [HttpPost("tests/{testId}/tasks")]
        public async Task<ActionResult<TestTaskDto>> AddTask(
            Guid testId,
            [FromBody] CreateTaskRequest request,
            CancellationToken cancellationToken)
        {
            if (!TryValidateTask(request, out var error))
                return BadRequest(error);

            var task = await _teachingCatalogService.AddTaskAsync(
                testId,
                request,
                cancellationToken);

            return task is null
                ? NotFound()
                : Ok(task);
        }

        [HttpPut("tests/{testId}/tasks/{taskId}")]
        public async Task<ActionResult<TestTaskDto>> UpdateTask(
            Guid testId,
            Guid taskId,
            [FromBody] CreateTaskRequest request,
            CancellationToken cancellationToken)
        {
            if (!TryValidateTask(request, out var error))
                return BadRequest(error);

            var task = await _teachingCatalogService.UpdateTaskAsync(
                testId,
                taskId,
                request,
                cancellationToken);

            return task is null
                ? NotFound()
                : Ok(task);
        }

        [HttpPatch("tests/{testId}/tasks/{taskId}/visibility")]
        public async Task<ActionResult<TestTaskDto>> SetTaskVisibility(
            Guid testId,
            Guid taskId,
            [FromBody] UpdateTaskVisibilityRequest request,
            CancellationToken cancellationToken)
        {
            var task = await _teachingCatalogService.SetTaskVisibilityAsync(
                testId,
                taskId,
                request.IsHidden,
                cancellationToken);

            return task is null
                ? NotFound()
                : Ok(task);
        }

        [HttpDelete("tests/{testId}/tasks/{taskId}")]
        public async Task<IActionResult> DeleteTask(
            Guid testId,
            Guid taskId,
            CancellationToken cancellationToken)
        {
            var deleted = await _teachingCatalogService.DeleteTaskAsync(
                testId,
                taskId,
                cancellationToken);

            return deleted
                ? NoContent()
                : NotFound();
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

            if (!request.Deadline.HasValue)
            {
                error = "Deadline is required.";
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

            if (request.Type == TestTaskType.MultipleChoice && request.WrongAnswerPenalty < 0)
            {
                error = "WrongAnswerPenalty must not be negative for multiple-choice tasks.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}

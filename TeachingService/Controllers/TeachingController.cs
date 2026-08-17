using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TeachingService.Contracts.Enums;
using TeachingService.Contracts.Models;
using TeachingService.Contracts.Requests;
using TeachingService.Enums;
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
            if (!TryGetTeacherUserId(out var teacherUserId))
                return Unauthorized("Teacher user id claim is required.");

            return Ok(await _teachingCatalogService.GetTeacherTestsAsync(
                teacherUserId,
                includeHidden: true,
                cancellationToken));
        }

        [HttpGet("tests/{testId}")]
        public async Task<ActionResult<CourseTestDto>> GetTest(
            Guid testId,
            CancellationToken cancellationToken)
        {
            if (!TryGetTeacherUserId(out var teacherUserId))
                return Unauthorized("Teacher user id claim is required.");

            var test = await _teachingCatalogService.GetTeacherTestAsync(
                testId,
                teacherUserId,
                includeHidden: true,
                cancellationToken);

            return test is null
                ? NotFound()
                : Ok(test);
        }

        [HttpGet("tests/{testId}/versions")]
        public async Task<ActionResult<List<CourseTestVersionSummaryDto>>> GetTestVersions(
            Guid testId,
            CancellationToken cancellationToken)
        {
            if (!TryGetTeacherUserId(out var teacherUserId))
                return Unauthorized("Teacher user id claim is required.");

            var versions = await _teachingCatalogService.GetTeacherTestVersionsAsync(
                testId,
                teacherUserId,
                cancellationToken);

            return versions is null
                ? NotFound()
                : Ok(versions);
        }

        [HttpGet("tests/{testId}/versions/{versionNumber:int}")]
        public async Task<ActionResult<CourseTestDto>> GetTestVersion(
            Guid testId,
            int versionNumber,
            CancellationToken cancellationToken)
        {
            if (!TryGetTeacherUserId(out var teacherUserId))
                return Unauthorized("Teacher user id claim is required.");

            var test = await _teachingCatalogService.GetTeacherTestVersionAsync(
                testId,
                versionNumber,
                teacherUserId,
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

            if (!TryGetTeacherUserId(out var teacherUserId))
                return Unauthorized("Teacher user id claim is required.");

            var test = await _teachingCatalogService.CreateTestAsync(
                request,
                teacherUserId,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetTestVersion),
                new { testId = test.Id, versionNumber = test.VersionNumber },
                test);
        }

        [HttpPost("tests/{testId}/versions")]
        public async Task<ActionResult<CourseTestDto>> CreateDraftVersion(
            Guid testId,
            [FromQuery] int? publishedVersionNumber,
            CancellationToken cancellationToken)
        {
            if (!TryGetTeacherUserId(out var teacherUserId))
                return Unauthorized("Teacher user id claim is required.");
            if (!publishedVersionNumber.HasValue || publishedVersionNumber.Value <= 0)
                return BadRequest("A positive publishedVersionNumber query parameter is required.");

            var result = await _teachingCatalogService.CreateDraftVersionAsync(
                testId,
                publishedVersionNumber.Value,
                teacherUserId,
                cancellationToken);
            if (result.Status != CatalogOperationStatus.Success)
                return ToTestOperationError(result);

            return CreatedAtAction(
                nameof(GetTestVersion),
                new
                {
                    testId,
                    versionNumber = result.Value!.VersionNumber
                },
                result.Value);
        }

        [HttpPut("tests/{testId}")]
        public async Task<ActionResult<CourseTestDto>> UpdateTest(
            Guid testId,
            [FromQuery] int versionNumber,
            [FromQuery] int? contentRevision,
            [FromBody] CreateTestRequest request,
            CancellationToken cancellationToken)
        {
            if (!TryValidateTest(request, out var error))
                return BadRequest(error);
            if (versionNumber <= 0)
                return BadRequest("A positive versionNumber query parameter is required.");
            if (!contentRevision.HasValue || contentRevision.Value < 0)
                return BadRequest("A non-negative contentRevision query parameter is required.");

            if (!TryGetTeacherUserId(out var teacherUserId))
                return Unauthorized("Teacher user id claim is required.");

            var test = await _teachingCatalogService.UpdateTestAsync(
                testId,
                versionNumber,
                contentRevision.Value,
                request,
                teacherUserId,
                cancellationToken);

            return test.Status == CatalogOperationStatus.Success
                ? Ok(test.Value)
                : ToTestOperationError(test);
        }

        [HttpPost("tests/{testId}/versions/{versionNumber:int}/publish")]
        public async Task<ActionResult<CourseTestDto>> PublishVersion(
            Guid testId,
            int versionNumber,
            [FromQuery] int? contentRevision,
            CancellationToken cancellationToken)
        {
            if (!TryGetTeacherUserId(out var teacherUserId))
                return Unauthorized("Teacher user id claim is required.");
            if (!contentRevision.HasValue || contentRevision.Value < 0)
                return BadRequest("A non-negative contentRevision query parameter is required.");

            var result = await _teachingCatalogService.PublishVersionAsync(
                testId,
                versionNumber,
                contentRevision.Value,
                teacherUserId,
                cancellationToken);

            return result.Status == CatalogOperationStatus.Success
                ? Ok(result.Value)
                : ToTestOperationError(result);
        }

        [HttpDelete("tests/{testId}/versions/{versionNumber:int}")]
        public async Task<IActionResult> DeleteDraftVersion(
            Guid testId,
            int versionNumber,
            [FromQuery] int? contentRevision,
            CancellationToken cancellationToken)
        {
            if (!TryGetTeacherUserId(out var teacherUserId))
                return Unauthorized("Teacher user id claim is required.");
            if (!contentRevision.HasValue || contentRevision.Value < 0)
                return BadRequest("A non-negative contentRevision query parameter is required.");

            var result = await _teachingCatalogService.DeleteDraftVersionAsync(
                testId,
                versionNumber,
                contentRevision.Value,
                teacherUserId,
                cancellationToken);

            return result.Status switch
            {
                CatalogOperationStatus.Success => NoContent(),
                CatalogOperationStatus.NotFound => NotFound(),
                CatalogOperationStatus.Conflict => Conflict(CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Version cannot be deleted",
                    result.Error)),
                _ => BadRequest(CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid version",
                    result.Error))
            };
        }

        [HttpPost("tests/{testId}/tasks")]
        public async Task<ActionResult<TestTaskDto>> AddTask(
            Guid testId,
            [FromQuery] int versionNumber,
            [FromQuery] int? contentRevision,
            [FromBody] CreateTaskRequest request,
            CancellationToken cancellationToken)
        {
            if (!TryValidateTask(request, out var error))
                return BadRequest(error);
            if (versionNumber <= 0)
                return BadRequest("A positive versionNumber query parameter is required.");
            if (!contentRevision.HasValue || contentRevision.Value < 0)
                return BadRequest("A non-negative contentRevision query parameter is required.");

            if (!TryGetTeacherUserId(out var teacherUserId))
                return Unauthorized("Teacher user id claim is required.");

            var task = await _teachingCatalogService.AddTaskAsync(
                testId,
                versionNumber,
                contentRevision.Value,
                request,
                teacherUserId,
                cancellationToken);

            return ToTaskOperationResult(task);
        }

        [HttpPut("tests/{testId}/tasks/{taskId}")]
        public async Task<ActionResult<TestTaskDto>> UpdateTask(
            Guid testId,
            Guid taskId,
            [FromQuery] int versionNumber,
            [FromQuery] int? contentRevision,
            [FromBody] CreateTaskRequest request,
            CancellationToken cancellationToken)
        {
            if (!TryValidateTask(request, out var error))
                return BadRequest(error);
            if (versionNumber <= 0)
                return BadRequest("A positive versionNumber query parameter is required.");
            if (!contentRevision.HasValue || contentRevision.Value < 0)
                return BadRequest("A non-negative contentRevision query parameter is required.");

            if (!TryGetTeacherUserId(out var teacherUserId))
                return Unauthorized("Teacher user id claim is required.");

            var task = await _teachingCatalogService.UpdateTaskAsync(
                testId,
                versionNumber,
                contentRevision.Value,
                taskId,
                request,
                teacherUserId,
                cancellationToken);

            return ToTaskOperationResult(task);
        }

        [HttpPatch("tests/{testId}/tasks/{taskId}/visibility")]
        public async Task<ActionResult<TestTaskDto>> SetTaskVisibility(
            Guid testId,
            Guid taskId,
            [FromQuery] int versionNumber,
            [FromQuery] int? contentRevision,
            [FromBody] UpdateTaskVisibilityRequest request,
            CancellationToken cancellationToken)
        {
            if (!TryGetTeacherUserId(out var teacherUserId))
                return Unauthorized("Teacher user id claim is required.");
            if (versionNumber <= 0)
                return BadRequest("A positive versionNumber query parameter is required.");
            if (!contentRevision.HasValue || contentRevision.Value < 0)
                return BadRequest("A non-negative contentRevision query parameter is required.");

            var task = await _teachingCatalogService.SetTaskVisibilityAsync(
                testId,
                versionNumber,
                contentRevision.Value,
                taskId,
                request.IsHidden,
                teacherUserId,
                cancellationToken);

            return ToTaskOperationResult(task);
        }

        [HttpDelete("tests/{testId}/tasks/{taskId}")]
        public async Task<IActionResult> DeleteTask(
            Guid testId,
            Guid taskId,
            [FromQuery] int versionNumber,
            [FromQuery] int? contentRevision,
            CancellationToken cancellationToken)
        {
            if (!TryGetTeacherUserId(out var teacherUserId))
                return Unauthorized("Teacher user id claim is required.");
            if (versionNumber <= 0)
                return BadRequest("A positive versionNumber query parameter is required.");
            if (!contentRevision.HasValue || contentRevision.Value < 0)
                return BadRequest("A non-negative contentRevision query parameter is required.");

            var result = await _teachingCatalogService.DeleteTaskAsync(
                testId,
                versionNumber,
                contentRevision.Value,
                taskId,
                teacherUserId,
                cancellationToken);

            return result.Status switch
            {
                CatalogOperationStatus.Success => NoContent(),
                CatalogOperationStatus.NotFound => NotFound(),
                CatalogOperationStatus.Conflict => Conflict(CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Task cannot be deleted",
                    result.Error)),
                _ => BadRequest(CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid task operation",
                    result.Error))
            };
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

            if (request.Status != CourseTestStatus.Draft)
            {
                error = "Tests are created and edited as drafts. Use the publish endpoint to publish a version.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private ActionResult<CourseTestDto> ToTestOperationError(
            Result<CatalogOperationStatus, CourseTestDto> result)
        {
            return result.Status switch
            {
                CatalogOperationStatus.NotFound => NotFound(),
                CatalogOperationStatus.Conflict => Conflict(CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Test version conflict",
                    result.Error)),
                CatalogOperationStatus.ValidationFailed => BadRequest(CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Test version cannot be published",
                    result.Error)),
                _ => BadRequest(CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid test operation",
                    result.Error))
            };
        }

        private ActionResult<TestTaskDto> ToTaskOperationResult(
            Result<CatalogOperationStatus, TestTaskDto> result)
        {
            return result.Status switch
            {
                CatalogOperationStatus.Success => Ok(result.Value),
                CatalogOperationStatus.NotFound => NotFound(),
                CatalogOperationStatus.Conflict => Conflict(CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Task version conflict",
                    result.Error)),
                _ => BadRequest(CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid task operation",
                    result.Error))
            };
        }

        private static ProblemDetails CreateProblem(
            int status,
            string title,
            string? detail)
        {
            return new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail
            };
        }

        private bool TryGetTeacherUserId(out string teacherUserId)
        {
            teacherUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? string.Empty;

            return !string.IsNullOrWhiteSpace(teacherUserId);
        }

        private static bool TryValidateTask(
            CreateTaskRequest request,
            out string error)
        {
            if (!Enum.IsDefined(request.Type))
            {
                error = "Type contains an unsupported value.";
                return false;
            }

            if (request.CheckMode.HasValue && !Enum.IsDefined(request.CheckMode.Value))
            {
                error = "CheckMode contains an unsupported value.";
                return false;
            }

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

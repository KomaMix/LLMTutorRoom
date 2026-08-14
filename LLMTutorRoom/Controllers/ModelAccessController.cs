using System.Security.Claims;
using LLMTutorRoom.DTOs;
using LLMTutorRoom.Services.ReviewProcessing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LLMTutorRoom.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/classroom/model-access")]
    public sealed class ModelAccessController : ControllerBase
    {
        private readonly TeacherModelAccessService _modelAccessService;

        public ModelAccessController(TeacherModelAccessService modelAccessService)
        {
            _modelAccessService = modelAccessService;
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("models")]
        public async Task<ActionResult<IReadOnlyCollection<LlmModelCatalogItemResponse>>> GetModelCatalog(
            CancellationToken cancellationToken)
        {
            return Ok(await _modelAccessService.GetModelCatalogAsync(cancellationToken));
        }

        [Authorize(Roles = "Teacher")]
        [HttpGet("me")]
        public async Task<ActionResult<IReadOnlyCollection<TeacherModelAccessResponse>>> GetMyModelAccess(
            CancellationToken cancellationToken)
        {
            return Ok(await _modelAccessService.GetTeacherAccessAsync(
                GetUserId(),
                includeDisabled: false,
                cancellationToken));
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("teachers/{teacherUserId}")]
        public async Task<ActionResult<IReadOnlyCollection<TeacherModelAccessResponse>>> GetTeacherModelAccess(
            [FromRoute] string teacherUserId,
            CancellationToken cancellationToken)
        {
            return Ok(await _modelAccessService.GetTeacherAccessAsync(
                teacherUserId,
                includeDisabled: true,
                cancellationToken));
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("models/{modelKey}/teachers/{teacherUserId}")]
        public async Task<ActionResult<TeacherModelAccessResponse>> UpsertTeacherModelAccess(
            [FromRoute] string modelKey,
            [FromRoute] string teacherUserId,
            [FromBody] UpsertTeacherModelAccessRequest request,
            CancellationToken cancellationToken)
        {
            var access = await _modelAccessService.UpsertTeacherAccessAsync(
                teacherUserId,
                modelKey,
                request,
                cancellationToken);

            return access is null
                ? NotFound()
                : Ok(access);
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("models/{modelKey}/teachers/{teacherUserId}")]
        public async Task<IActionResult> DeleteTeacherModelAccess(
            [FromRoute] string modelKey,
            [FromRoute] string teacherUserId,
            CancellationToken cancellationToken)
        {
            var deleted = await _modelAccessService.DeleteTeacherAccessAsync(
                teacherUserId,
                modelKey,
                cancellationToken);

            return deleted
                ? NoContent()
                : NotFound();
        }

        private string GetUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? string.Empty;
        }
    }
}

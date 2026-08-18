using System.Security.Claims;
using LLMTutorRoom.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReviewService.Contracts.Responses;
using PublicUpsertTeacherModelAccessRequest = LLMTutorRoom.DTOs.UpsertTeacherModelAccessRequest;
using ReviewUpsertTeacherModelAccessRequest = ReviewService.Contracts.Requests.UpsertTeacherModelAccessRequest;

namespace LLMTutorRoom.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/classroom/model-access")]
    public sealed class ModelAccessController : ControllerBase
    {
        private readonly IReviewServiceClient _reviewServiceClient;

        public ModelAccessController(IReviewServiceClient reviewServiceClient)
        {
            _reviewServiceClient = reviewServiceClient;
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("models")]
        public async Task<ActionResult<List<LlmModelCatalogItemResponse>>> GetModelCatalog(
            CancellationToken cancellationToken)
        {
            return Ok(await _reviewServiceClient.GetModelCatalogAsync(cancellationToken));
        }

        [Authorize(Roles = "Teacher")]
        [HttpGet("me")]
        public async Task<ActionResult<List<TeacherModelAccessResponse>>> GetMyModelAccess(
            CancellationToken cancellationToken)
        {
            return Ok(await _reviewServiceClient.GetTeacherModelAccessAsync(
                GetUserId(),
                includeDisabled: false,
                cancellationToken));
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("teachers/{teacherUserId}")]
        public async Task<ActionResult<List<TeacherModelAccessResponse>>> GetTeacherModelAccess(
            [FromRoute] string teacherUserId,
            CancellationToken cancellationToken)
        {
            return Ok(await _reviewServiceClient.GetTeacherModelAccessAsync(
                teacherUserId,
                includeDisabled: true,
                cancellationToken));
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("models/{modelKey}/teachers/{teacherUserId}")]
        public async Task<ActionResult<TeacherModelAccessResponse>> UpsertTeacherModelAccess(
            [FromRoute] string modelKey,
            [FromRoute] string teacherUserId,
            [FromBody] PublicUpsertTeacherModelAccessRequest request,
            CancellationToken cancellationToken)
        {
            var access = await _reviewServiceClient.UpsertTeacherModelAccessAsync(
                teacherUserId,
                modelKey,
                new ReviewUpsertTeacherModelAccessRequest
                {
                    IsEnabled = request.IsEnabled,
                    PeriodSeconds = request.PeriodSeconds,
                    MaxChecks = request.MaxChecks
                },
                cancellationToken);

            if (access.IsSuccess)
            {
                return access.Value is null
                    ? StatusCode(StatusCodes.Status502BadGateway)
                    : Ok(access.Value);
            }

            return StatusCode((int)access.StatusCode, access.Error);
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("models/{modelKey}/teachers/{teacherUserId}")]
        public async Task<IActionResult> DeleteTeacherModelAccess(
            [FromRoute] string modelKey,
            [FromRoute] string teacherUserId,
            CancellationToken cancellationToken)
        {
            var deleted = await _reviewServiceClient.DeleteTeacherModelAccessAsync(
                teacherUserId,
                modelKey,
                cancellationToken);

            if (deleted.IsSuccess)
                return NoContent();

            return StatusCode((int)deleted.StatusCode, deleted.Error);
        }

        private string GetUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? string.Empty;
        }
    }
}

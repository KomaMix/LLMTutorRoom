using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReviewService.Contracts.Requests;
using ReviewService.Contracts.Responses;
using ReviewService.Interfaces;

namespace ReviewService.Controllers;

[ApiController]
[Authorize]
[Route("api/model-access")]
public sealed class ModelAccessController(
    ITeacherModelAccessService modelAccessService) : ControllerBase
{
    [Authorize(Roles = "Admin")]
    [HttpGet("models")]
    public async Task<ActionResult<List<LlmModelCatalogItemResponse>>> GetCatalog(
        CancellationToken cancellationToken)
    {
        return Ok(await modelAccessService.GetModelCatalogAsync(cancellationToken));
    }

    [Authorize(Roles = "Teacher")]
    [HttpGet("me")]
    public async Task<ActionResult<List<TeacherModelAccessResponse>>> GetMyAccess(
        CancellationToken cancellationToken)
    {
        var teacherUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(teacherUserId))
            return Unauthorized();

        return Ok(await modelAccessService.GetTeacherAccessAsync(
            teacherUserId,
            includeDisabled: false,
            cancellationToken));
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("teachers/{teacherId}")]
    public async Task<ActionResult<List<TeacherModelAccessResponse>>> GetTeacherAccess(
        [FromRoute] string teacherId,
        CancellationToken cancellationToken)
    {
        return Ok(await modelAccessService.GetTeacherAccessAsync(
            teacherId,
            includeDisabled: true,
            cancellationToken));
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("models/{modelKey}/teachers/{teacherId}")]
    public async Task<ActionResult<TeacherModelAccessResponse>> UpsertTeacherAccess(
        [FromRoute] string modelKey,
        [FromRoute] string teacherId,
        [FromBody] UpsertTeacherModelAccessRequest request,
        CancellationToken cancellationToken)
    {
        var response = await modelAccessService.UpsertTeacherAccessAsync(
            teacherId,
            modelKey,
            request,
            cancellationToken);
        return response is null
            ? Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "The model does not exist in LLMGateway.")
            : Ok(response);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("models/{modelKey}/teachers/{teacherId}")]
    public async Task<IActionResult> DeleteTeacherAccess(
        [FromRoute] string modelKey,
        [FromRoute] string teacherId,
        CancellationToken cancellationToken)
    {
        return await modelAccessService.DeleteTeacherAccessAsync(
            teacherId,
            modelKey,
            cancellationToken)
            ? NoContent()
            : Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Teacher model access was not found.");
    }
}

using Microsoft.AspNetCore.Mvc;
using ReviewService.Contracts.Requests;
using ReviewService.Contracts.Responses;
using ReviewService.Interfaces;

namespace ReviewService.Controllers;

[ApiController]
[Route("internal/model-access")]
public sealed class ModelAccessController(
    ITeacherModelAccessService modelAccessService) : ControllerBase
{
    [HttpGet("catalog")]
    public async Task<ActionResult<List<LlmModelCatalogItemResponse>>> GetCatalog(
        CancellationToken cancellationToken)
    {
        return Ok(await modelAccessService.GetModelCatalogAsync(cancellationToken));
    }

    [HttpGet("teachers/{teacherUserId}")]
    public async Task<ActionResult<List<TeacherModelAccessResponse>>> GetTeacherAccess(
        [FromRoute] string teacherUserId,
        [FromQuery] bool includeDisabled = false,
        CancellationToken cancellationToken = default)
    {
        return Ok(await modelAccessService.GetTeacherAccessAsync(
            teacherUserId,
            includeDisabled,
            cancellationToken));
    }

    [HttpPut("teachers/{teacherUserId}/models/{modelKey}")]
    public async Task<ActionResult<TeacherModelAccessResponse>> UpsertTeacherAccess(
        [FromRoute] string teacherUserId,
        [FromRoute] string modelKey,
        [FromBody] UpsertTeacherModelAccessRequest request,
        CancellationToken cancellationToken)
    {
        var response = await modelAccessService.UpsertTeacherAccessAsync(
            teacherUserId,
            modelKey,
            request,
            cancellationToken);
        return response is null
            ? Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "The model does not exist in LLMGateway.")
            : Ok(response);
    }

    [HttpDelete("teachers/{teacherUserId}/models/{modelKey}")]
    public async Task<IActionResult> DeleteTeacherAccess(
        [FromRoute] string teacherUserId,
        [FromRoute] string modelKey,
        CancellationToken cancellationToken)
    {
        return await modelAccessService.DeleteTeacherAccessAsync(
            teacherUserId,
            modelKey,
            cancellationToken)
            ? NoContent()
            : Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Teacher model access was not found.");
    }
}

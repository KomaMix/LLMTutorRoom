using Microsoft.AspNetCore.Mvc;
using ReviewService.Contracts.Responses;
using ReviewService.Interfaces;

namespace ReviewService.Controllers;

[ApiController]
[Route("internal/model-access")]
public sealed class InternalModelAccessController(
    ITeacherModelAccessService modelAccessService) : ControllerBase
{
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
}

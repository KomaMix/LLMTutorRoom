using AttemptService.Contracts.Responses;
using AttemptService.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AttemptService.Controllers;

[ApiController]
[Route("internal/attempts")]
public sealed class InternalAttemptsController(IAttemptLifecycleService attemptService)
    : ControllerBase
{
    [HttpGet("students/{studentUserId}")]
    public async Task<ActionResult<List<TestAttemptResponse>>> GetStudentAttempts(
        string studentUserId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(studentUserId))
            return BadRequest();

        return Ok(await attemptService.GetStudentAttemptsAsync(
            studentUserId,
            cancellationToken));
    }
}

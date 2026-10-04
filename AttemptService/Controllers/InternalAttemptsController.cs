using AttemptService.Contracts.Responses;
using AttemptService.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AttemptService.Controllers;

[ApiController]
[Route("internal/attempts")]
public class InternalAttemptsController : ControllerBase
{
    private readonly IAttemptLifecycleService _attemptService;

    public InternalAttemptsController(IAttemptLifecycleService attemptService)
    {
        _attemptService = attemptService;
    }

    [HttpGet("students/{studentUserId}")]
    public async Task<ActionResult<List<TestAttemptResponse>>> GetStudentAttempts(
        string studentUserId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(studentUserId))
            return BadRequest();

        return Ok(await _attemptService.GetStudentAttemptsAsync(
            studentUserId,
            cancellationToken));
    }

    [HttpGet("teachers/{teacherUserId}")]
    public async Task<ActionResult<List<TestAttemptResponse>>> GetTeacherAttempts(
        string teacherUserId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(teacherUserId))
        {
            return BadRequest();
        }

        return Ok(await _attemptService.GetTeacherAttemptsAsync(
            teacherUserId,
            cancellationToken));
    }
}

using System.Security.Claims;
using AttemptService.Contracts.Requests;
using AttemptService.Contracts.Responses;
using AttemptService.Enums;
using AttemptService.Interfaces;
using AttemptService.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AttemptService.Controllers;

[ApiController]
[Authorize(Roles = "Student")]
[Route("api/attempts")]
public class AttemptsController : ControllerBase
{
    private readonly IAttemptLifecycleService _attemptService;

    public AttemptsController(IAttemptLifecycleService attemptService)
    {
        _attemptService = attemptService;
    }

    [HttpGet]
    public async Task<ActionResult<List<TestAttemptResponse>>> GetAttempts(
        CancellationToken cancellationToken)
    {
        var studentUserId = GetUserId();
        if (string.IsNullOrWhiteSpace(studentUserId))
            return Unauthorized();

        return Ok(await _attemptService.GetStudentAttemptsAsync(
            studentUserId,
            cancellationToken));
    }

    [HttpGet("{attemptId:guid}")]
    public async Task<ActionResult<TestAttemptResponse>> GetAttempt(
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var studentUserId = GetUserId();
        if (string.IsNullOrWhiteSpace(studentUserId))
            return Unauthorized();

        var attempt = await _attemptService.GetAttemptAsync(
            attemptId,
            studentUserId,
            cancellationToken);
        return attempt is null ? NotFound() : Ok(attempt);
    }

    [HttpPost("assignments/{assignmentId:guid}/start")]
    public async Task<ActionResult<TestAttemptResponse>> StartAttempt(
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        var studentUserId = GetUserId();
        if (string.IsNullOrWhiteSpace(studentUserId))
            return Unauthorized();

        var result = await _attemptService.StartAttemptAsync(
            assignmentId,
            studentUserId,
            GetUserName(),
            cancellationToken);

        return result.Status switch
        {
            AttemptOperationStatus.Created when result.Value is not null =>
                Created($"/api/attempts/{result.Value.Id}", result.Value),
            AttemptOperationStatus.Success when result.Value is not null => Ok(result.Value),
            AttemptOperationStatus.NotFound => NotFound(),
            _ => Conflict(CreateWriteConflictProblem())
        };
    }

    [HttpPut("{attemptId:guid}/answers")]
    public async Task<ActionResult<TestAttemptResponse>> SaveAnswers(
        Guid attemptId,
        [FromBody] SaveAttemptAnswersRequest request,
        CancellationToken cancellationToken)
    {
        var studentUserId = GetUserId();
        if (string.IsNullOrWhiteSpace(studentUserId))
            return Unauthorized();

        var result = await _attemptService.SaveAnswersAsync(
            attemptId,
            studentUserId,
            request.Answers ?? new Dictionary<string, string>(),
            cancellationToken);
        return ToMutationResponse(result);
    }

    [HttpPost("{attemptId:guid}/submit")]
    public async Task<ActionResult<TestAttemptResponse>> SubmitAttempt(
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var studentUserId = GetUserId();
        if (string.IsNullOrWhiteSpace(studentUserId))
            return Unauthorized();

        var result = await _attemptService.SubmitAttemptAsync(
            attemptId,
            studentUserId,
            GetUserName(),
            cancellationToken);
        return ToMutationResponse(result);
    }

    private ActionResult<TestAttemptResponse> ToMutationResponse(
        Result<AttemptOperationStatus, TestAttemptResponse> result)
    {
        if (result.Status == AttemptOperationStatus.Success && result.Value is not null)
            return Ok(result.Value);

        if (result.Status == AttemptOperationStatus.NotFound)
            return NotFound();

        if (result.Status == AttemptOperationStatus.InvalidInput)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid answers",
                Detail = "The submitted answers exceed the configured input limits."
            });
        }

        if (result.Status == AttemptOperationStatus.Conflict && result.Value is not null)
            return Conflict(result.Value);

        return Conflict(CreateWriteConflictProblem());
    }

    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    }

    private string GetUserName()
    {
        return User.FindFirstValue(ClaimTypes.Name) ?? "Студент";
    }

    private static ProblemDetails CreateWriteConflictProblem()
    {
        return new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Attempt changed concurrently",
            Detail = "The attempt changed repeatedly. Reload its current state and try again."
        };
    }
}

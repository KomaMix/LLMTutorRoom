using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeachingService.Contracts.Models;
using TeachingService.Contracts.Requests;
using TeachingService.Contracts.Responses;
using TeachingService.Enums;
using TeachingService.Services;

namespace TeachingService.Controllers;

[ApiController]
[Authorize(Roles = "Teacher,Admin")]
[Route("api/teaching")]
public class TestAssignmentsController : ControllerBase
{
    private readonly TestAssignmentService _assignmentService;

    public TestAssignmentsController(TestAssignmentService assignmentService)
    {
        _assignmentService = assignmentService;
    }

    [HttpGet("groups/{groupId:guid}/assignments")]
    public async Task<ActionResult<List<TestAssignmentResponse>>> GetAssignments(
        Guid groupId,
        CancellationToken cancellationToken)
    {
        var teacherUserId = User.IsInRole("Admin") ? null : GetCurrentUserId();
        if (!User.IsInRole("Admin") && string.IsNullOrWhiteSpace(teacherUserId))
        {
            return Unauthorized();
        }

        var assignments = await _assignmentService.GetGroupAssignmentsAsync(groupId, teacherUserId, cancellationToken);
        return assignments is null ? NotFound() : Ok(assignments);
    }

    [HttpGet("assignments/{assignmentId:guid}")]
    public async Task<ActionResult<TestAssignmentResponse>> GetAssignment(
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        var teacherUserId = GetCurrentUserId();
        if (!User.IsInRole("Admin") && string.IsNullOrWhiteSpace(teacherUserId))
        {
            return Unauthorized();
        }

        var assignment = await _assignmentService.GetAssignmentAsync(assignmentId, includeHidden: true, cancellationToken);
        return assignment is null || (!User.IsInRole("Admin") && assignment.TeacherUserId != teacherUserId)
            ? NotFound()
            : Ok(assignment);
    }

    [HttpGet("groups/{groupId:guid}/assignable-tests")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<List<CourseTestDto>>> GetAssignableTests(
        Guid groupId,
        CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Teacher"))
        {
            return Forbid();
        }

        var teacherUserId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(teacherUserId))
        {
            return Unauthorized();
        }

        var tests = await _assignmentService.GetAssignableTestsAsync(groupId, teacherUserId, cancellationToken);
        return tests is null ? NotFound() : Ok(tests);
    }

    [HttpPost("groups/{groupId:guid}/assignments")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<TestAssignmentResponse>> CreateAssignment(
        Guid groupId,
        [FromBody] CreateTestAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Teacher"))
        {
            return Forbid();
        }

        var teacherUserId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(teacherUserId))
        {
            return Unauthorized();
        }

        var result = await _assignmentService.CreateAssignmentAsync(groupId, request, teacherUserId, cancellationToken);
        return result.Status switch
        {
            AssignmentOperationStatus.Success => CreatedAtAction(nameof(GetAssignment),
                new { assignmentId = result.Value!.Id }, result.Value),
            AssignmentOperationStatus.NotFound => NotFound(),
            _ => BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid assignment",
                Detail = result.Error
            })
        };
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}

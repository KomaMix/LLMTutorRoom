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
[Route("api/teaching/groups")]
public class StudyGroupsController : ControllerBase
{
    private readonly StudyGroupService _studyGroupService;

    public StudyGroupsController(StudyGroupService studyGroupService)
    {
        _studyGroupService = studyGroupService;
    }

    [HttpGet]
    public async Task<ActionResult<List<StudyGroupResponse>>> GetGroups(
        CancellationToken cancellationToken)
    {
        if (!TryGetTeacherScope(out var teacherUserId))
        {
            return Unauthorized();
        }

        return Ok(await _studyGroupService.GetGroupsAsync(
            teacherUserId,
            cancellationToken));
    }

    [HttpGet("{groupId:guid}")]
    public async Task<ActionResult<StudyGroupResponse>> GetGroup(
        Guid groupId,
        CancellationToken cancellationToken)
    {
        if (!TryGetTeacherScope(out var teacherUserId))
        {
            return Unauthorized();
        }

        var group = await _studyGroupService.GetGroupAsync(
            groupId,
            teacherUserId,
            includeHidden: true,
            cancellationToken);
        return group is null ? NotFound() : Ok(group);
    }

    [HttpPost]
    public async Task<ActionResult<StudyGroupResponse>> CreateGroup(
        [FromBody] CreateStudyGroupRequest request,
        CancellationToken cancellationToken)
    {
        var teacherUserId = User.IsInRole("Admin")
            ? request.TeacherUserId?.Trim()
            : GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(teacherUserId))
        {
            return BadRequest(CreateProblem(
                StatusCodes.Status400BadRequest,
                "Invalid group",
                "TeacherUserId is required."));
        }

        var result = await _studyGroupService.CreateGroupAsync(
            request,
            teacherUserId,
            cancellationToken);
        return result.Status == GroupOperationStatus.Success
            ? CreatedAtAction(nameof(GetGroup), new { groupId = result.Value!.Id }, result.Value)
            : ToGroupError(result);
    }

    [HttpPut("{groupId:guid}")]
    public async Task<ActionResult<StudyGroupResponse>> UpdateGroup(
        Guid groupId,
        [FromBody] UpdateStudyGroupRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetTeacherScope(out var teacherUserId))
        {
            return Unauthorized();
        }

        var result = await _studyGroupService.UpdateGroupAsync(
            groupId,
            request,
            teacherUserId,
            cancellationToken);
        return result.Status == GroupOperationStatus.Success
            ? Ok(result.Value)
            : ToGroupError(result);
    }

    [HttpGet("{groupId:guid}/assignable-tests")]
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

        var tests = await _studyGroupService.GetAssignableTestsAsync(
            groupId,
            teacherUserId,
            cancellationToken);
        return tests is null ? NotFound() : Ok(tests);
    }

    [HttpPost("{groupId:guid}/assignments")]
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

        var result = await _studyGroupService.CreateAssignmentAsync(
            groupId,
            request,
            teacherUserId,
            cancellationToken);
        return result.Status == GroupOperationStatus.Success
            ? CreatedAtAction(nameof(GetGroup), new { groupId }, result.Value)
            : ToAssignmentError(result);
    }

    private bool TryGetTeacherScope(out string? teacherUserId)
    {
        if (User.IsInRole("Admin"))
        {
            teacherUserId = null;
            return true;
        }

        teacherUserId = GetCurrentUserId();
        return !string.IsNullOrWhiteSpace(teacherUserId);
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    private ActionResult<StudyGroupResponse> ToGroupError(
        Result<GroupOperationStatus, StudyGroupResponse> result)
    {
        return result.Status switch
        {
            GroupOperationStatus.NotFound => NotFound(),
            GroupOperationStatus.Conflict => Conflict(CreateProblem(
                StatusCodes.Status409Conflict,
                "Group conflict",
                result.Error)),
            _ => BadRequest(CreateProblem(
                StatusCodes.Status400BadRequest,
                "Invalid group",
                result.Error))
        };
    }

    private ActionResult<TestAssignmentResponse> ToAssignmentError(
        Result<GroupOperationStatus, TestAssignmentResponse> result)
    {
        return result.Status switch
        {
            GroupOperationStatus.NotFound => NotFound(),
            GroupOperationStatus.Conflict => Conflict(CreateProblem(
                StatusCodes.Status409Conflict,
                "Assignment conflict",
                result.Error)),
            _ => BadRequest(CreateProblem(
                StatusCodes.Status400BadRequest,
                "Invalid assignment",
                result.Error))
        };
    }

    private static ProblemDetails CreateProblem(int status, string title, string? detail)
    {
        return new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail
        };
    }
}

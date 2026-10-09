using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GroupService.Contracts.Requests;
using GroupService.Contracts.Responses;
using GroupService.Enums;
using GroupService.Services;

namespace GroupService.Controllers;

[ApiController]
[Authorize(Roles = "Teacher,Admin")]
[Route("api/groups")]
public class StudyGroupsController : ControllerBase
{
    private readonly StudyGroupService _studyGroupService;

    public StudyGroupsController(StudyGroupService studyGroupService)
    {
        _studyGroupService = studyGroupService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<StudyGroupResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<StudyGroupResponse>>> GetGroups(CancellationToken cancellationToken)
    {
        if (!TryGetTeacherScope(out var teacherUserId))
        {
            return Unauthorized();
        }

        return Ok(await _studyGroupService.GetGroupsAsync(teacherUserId, cancellationToken));
    }

    [HttpGet("{groupId:guid}")]
    [ProducesResponseType(typeof(StudyGroupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StudyGroupResponse>> GetGroup(
        Guid groupId,
        CancellationToken cancellationToken)
    {
        if (!TryGetTeacherScope(out var teacherUserId))
        {
            return Unauthorized();
        }

        var group = await _studyGroupService.GetGroupAsync(groupId, teacherUserId, cancellationToken);
        return group is null ? NotFound() : Ok(group);
    }

    [HttpPost]
    [ProducesResponseType(typeof(StudyGroupResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<StudyGroupResponse>> CreateGroup(
        [FromBody] CreateStudyGroupRequest request,
        CancellationToken cancellationToken)
    {
        var isAdmin = User.IsInRole("Admin");
        var teacherUserId = isAdmin ? request.TeacherUserId : GetCurrentUserId();
        if (!isAdmin && string.IsNullOrWhiteSpace(teacherUserId))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(teacherUserId))
        {
            return BadRequest(CreateProblem(
                StatusCodes.Status400BadRequest,
                "Invalid group",
                "TeacherUserId is required."));
        }

        var result = await _studyGroupService.CreateGroupAsync(request, teacherUserId, cancellationToken);
        return result.Status == GroupOperationStatus.Success
            ? CreatedAtAction(nameof(GetGroup), new { groupId = result.Value!.Id }, result.Value)
            : ToGroupError(result);
    }

    [HttpPut("{groupId:guid}")]
    [ProducesResponseType(typeof(StudyGroupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StudyGroupResponse>> UpdateGroup(
        Guid groupId,
        [FromBody] UpdateStudyGroupRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetTeacherScope(out var teacherUserId))
        {
            return Unauthorized();
        }

        var result = await _studyGroupService.UpdateGroupAsync(groupId, request, teacherUserId, cancellationToken);
        return result.Status == GroupOperationStatus.Success ? Ok(result.Value) : ToGroupError(result);
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

    private ActionResult<StudyGroupResponse> ToGroupError(Result<GroupOperationStatus, StudyGroupResponse> result)
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

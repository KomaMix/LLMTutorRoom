using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GroupService.Contracts.Responses;
using GroupService.Services;

namespace GroupService.Controllers;

[ApiController]
[AllowAnonymous]
[Route("internal")]
public class InternalGroupsController : ControllerBase
{
    private readonly StudyGroupService _studyGroupService;

    public InternalGroupsController(StudyGroupService studyGroupService)
    {
        _studyGroupService = studyGroupService;
    }

    [HttpGet("groups/{groupId:guid}")]
    [ProducesResponseType(typeof(StudyGroupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StudyGroupResponse>> GetGroup(
        Guid groupId,
        CancellationToken cancellationToken)
    {
        var group = await _studyGroupService.GetGroupAsync(groupId, teacherUserId: null, cancellationToken);
        return group is null ? NotFound() : Ok(group);
    }

    [HttpGet("teachers/{teacherUserId}/groups")]
    [ProducesResponseType(typeof(List<StudyGroupResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<StudyGroupResponse>>> GetTeacherGroups(
        string teacherUserId,
        CancellationToken cancellationToken)
    {
        return Ok(await _studyGroupService.GetGroupsAsync(teacherUserId, cancellationToken));
    }

    [HttpGet("students/{studentUserId}/groups")]
    [ProducesResponseType(typeof(List<StudyGroupResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<StudyGroupResponse>>> GetStudentGroups(
        string studentUserId,
        CancellationToken cancellationToken)
    {
        return Ok(await _studyGroupService.GetStudentGroupsAsync(studentUserId, cancellationToken));
    }
}

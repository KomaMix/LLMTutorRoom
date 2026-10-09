using Microsoft.AspNetCore.Mvc;
using TeachingService.Contracts.Models;
using TeachingService.Contracts.Responses;
using TeachingService.Services;

namespace TeachingService.Controllers;

[ApiController]
[Route("internal/teaching")]
public class InternalTeachingController : ControllerBase
{
    private readonly TeachingCatalogService _teachingCatalogService;
    private readonly TestAssignmentService _assignmentService;

    public InternalTeachingController(TeachingCatalogService teachingCatalogService, TestAssignmentService assignmentService)
    {
        _teachingCatalogService = teachingCatalogService;
        _assignmentService = assignmentService;
    }

    [HttpGet("tests")]
    public async Task<ActionResult<List<CourseTestDto>>> GetTests(
        [FromQuery] bool publishedOnly,
        [FromQuery] bool includeHidden,
        CancellationToken cancellationToken)
    {
        return Ok(await _teachingCatalogService.GetAllTestsAsync(publishedOnly, includeHidden, cancellationToken));
    }

    [HttpGet("tests/{testId}")]
    public async Task<ActionResult<CourseTestDto>> GetTest(
        Guid testId,
        [FromQuery] bool includeHidden,
        [FromQuery] int? versionNumber,
        CancellationToken cancellationToken)
    {
        var test = await _teachingCatalogService.GetTestAsync(testId, versionNumber, includeHidden, cancellationToken);
        return test is null ? NotFound() : Ok(test);
    }

    [HttpGet("students/{studentUserId}/assignments")]
    public async Task<ActionResult<List<TestAssignmentResponse>>> GetStudentAssignments(
        string studentUserId,
        CancellationToken cancellationToken)
    {
        return Ok(await _assignmentService.GetStudentAssignmentsAsync(studentUserId, cancellationToken));
    }

    [HttpGet("students/{studentUserId}/assignments/{assignmentId:guid}")]
    public async Task<ActionResult<TestAssignmentResponse>> GetStudentAssignment(
        string studentUserId,
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        var assignment = await _assignmentService.GetStudentAssignmentAsync(assignmentId, studentUserId, cancellationToken);
        return assignment is null ? NotFound() : Ok(assignment);
    }

    [HttpGet("assignments/{assignmentId:guid}")]
    public async Task<ActionResult<TestAssignmentResponse>> GetAssignment(
        Guid assignmentId,
        [FromQuery] bool includeHidden,
        CancellationToken cancellationToken)
    {
        var assignment = await _assignmentService.GetAssignmentAsync(assignmentId, includeHidden, cancellationToken);
        return assignment is null ? NotFound() : Ok(assignment);
    }

    [HttpGet("teachers/{teacherUserId}/assignments")]
    public async Task<ActionResult<List<TestAssignmentResponse>>> GetTeacherAssignments(
        string teacherUserId,
        CancellationToken cancellationToken)
    {
        return Ok(await _assignmentService.GetTeacherAssignmentsAsync(teacherUserId, cancellationToken));
    }
}

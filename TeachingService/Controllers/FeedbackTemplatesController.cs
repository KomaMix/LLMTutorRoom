using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeachingService.Contracts.Models;
using TeachingService.Contracts.Requests;
using TeachingService.Enums;
using TeachingService.Services;

namespace TeachingService.Controllers;

[ApiController]
[Authorize(Roles = "Teacher")]
[Route("api/teaching/feedback-templates")]
public class FeedbackTemplatesController : ControllerBase
{
    private readonly FeedbackTemplateService _feedbackTemplateService;

    public FeedbackTemplatesController(FeedbackTemplateService feedbackTemplateService)
    {
        _feedbackTemplateService = feedbackTemplateService;
    }

    [HttpGet]
    public async Task<ActionResult<List<FeedbackTemplateDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        if (!TryGetTeacherUserId(out var teacherUserId))
        {
            return Unauthorized();
        }

        return Ok(await _feedbackTemplateService.GetAllAsync(teacherUserId, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FeedbackTemplateDto>> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetTeacherUserId(out var teacherUserId))
        {
            return Unauthorized();
        }

        var template = await _feedbackTemplateService.GetAvailableAsync(
            id,
            teacherUserId,
            cancellationToken);
        return template is null ? NotFound() : Ok(template);
    }

    [HttpPost]
    public async Task<ActionResult<FeedbackTemplateDto>> Create(
        [FromBody] SaveFeedbackTemplateRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetTeacherUserId(out var teacherUserId))
        {
            return Unauthorized();
        }

        var result = await _feedbackTemplateService.CreateAsync(
            request,
            teacherUserId,
            cancellationToken);
        if (result.Status != CatalogOperationStatus.Success)
        {
            return ToError(result.Status, result.Error);
        }

        return CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<FeedbackTemplateDto>> Update(
        Guid id,
        [FromBody] SaveFeedbackTemplateRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetTeacherUserId(out var teacherUserId))
        {
            return Unauthorized();
        }

        var result = await _feedbackTemplateService.UpdateAsync(
            id,
            request,
            teacherUserId,
            cancellationToken);
        return result.Status == CatalogOperationStatus.Success
            ? Ok(result.Value)
            : ToError(result.Status, result.Error);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetTeacherUserId(out var teacherUserId))
        {
            return Unauthorized();
        }

        var result = await _feedbackTemplateService.DeleteAsync(
            id,
            teacherUserId,
            cancellationToken);
        return result.Status == CatalogOperationStatus.Success
            ? NoContent()
            : ToError(result.Status, result.Error);
    }

    private ObjectResult ToError(CatalogOperationStatus status, string? detail)
    {
        var statusCode = status switch
        {
            CatalogOperationStatus.NotFound => StatusCodes.Status404NotFound,
            CatalogOperationStatus.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };
        return Problem(
            statusCode: statusCode,
            title: status == CatalogOperationStatus.Conflict
                ? "Шаблон изменен"
                : "Не удалось сохранить шаблон",
            detail: detail);
    }

    private bool TryGetTeacherUserId(out string teacherUserId)
    {
        teacherUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        return !string.IsNullOrWhiteSpace(teacherUserId);
    }
}

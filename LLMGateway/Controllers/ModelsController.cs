using LLMGateway.DTOs.Models;
using LLMGateway.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LLMGateway.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/models")]
public sealed class ModelsController(IModelManagementService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ModelResponse>>> GetModels(CancellationToken ct) => Ok(await service.GetModelsAsync(ct));

    [HttpGet("{modelId:int}")]
    public async Task<ActionResult<ModelResponse>> GetModel(int modelId, CancellationToken ct) =>
        await service.GetModelAsync(modelId, ct) is { } result ? Ok(result) : NotFound();

    [HttpPost]
    public async Task<ActionResult<ModelResponse>> CreateModel(CreateModelRequest request, CancellationToken ct)
    {
        var result = await service.CreateModelAsync(request, ct);
        return CreatedAtAction(nameof(GetModel), new { modelId = result.Id }, result);
    }

    [HttpPut("{modelId:int}")]
    public async Task<ActionResult<ModelResponse>> UpdateModel(int modelId, UpdateModelRequest request, CancellationToken ct) =>
        await service.UpdateModelAsync(modelId, request, ct) is { } result ? Ok(result) : NotFound();

    [HttpGet("deployments/{deploymentId:int}")]
    public async Task<ActionResult<ModelDeploymentResponse>> GetDeployment(int deploymentId, CancellationToken ct) =>
        await service.GetDeploymentAsync(deploymentId, ct) is { } result ? Ok(result) : NotFound();

    [HttpPost("{modelId:int}/deployments")]
    public async Task<ActionResult<ModelDeploymentResponse>> CreateDeployment(int modelId, CreateModelDeploymentRequest request, CancellationToken ct)
    {
        var result = await service.CreateDeploymentAsync(modelId, request, ct);
        return CreatedAtAction(nameof(GetDeployment), new { deploymentId = result.Id }, result);
    }

    [HttpPut("deployments/{deploymentId:int}")]
    public async Task<ActionResult<ModelDeploymentResponse>> UpdateDeployment(int deploymentId, UpdateModelDeploymentRequest request, CancellationToken ct) =>
        await service.UpdateDeploymentAsync(deploymentId, request, ct) is { } result ? Ok(result) : NotFound();

    [HttpDelete("deployments/{deploymentId:int}")]
    public async Task<IActionResult> DeleteDeployment(int deploymentId, CancellationToken ct) =>
        await service.DeleteDeploymentAsync(deploymentId, ct) ? NoContent() : NotFound();

    [HttpPost("deployments/{deploymentId:int}/check")]
    public async Task<ActionResult<DeploymentCheckResponse>> CheckDeployment(int deploymentId, CancellationToken ct) =>
        await service.CheckDeploymentAsync(deploymentId, ct) is { } result ? Ok(result) : NotFound();

    [HttpGet("rate-limits/{ruleId:int}")]
    public async Task<ActionResult<RateLimitRuleResponse>> GetRateLimit(int ruleId, CancellationToken ct) =>
        await service.GetRateLimitAsync(ruleId, ct) is { } result ? Ok(result) : NotFound();

    [HttpPost("deployments/{deploymentId:int}/rate-limits")]
    public async Task<ActionResult<RateLimitRuleResponse>> CreateRateLimit(int deploymentId, CreateRateLimitRuleRequest request, CancellationToken ct)
    {
        var result = await service.CreateRateLimitAsync(deploymentId, request, ct);
        return CreatedAtAction(nameof(GetRateLimit), new { ruleId = result.Id }, result);
    }

    [HttpPut("rate-limits/{ruleId:int}")]
    public async Task<ActionResult<RateLimitRuleResponse>> UpdateRateLimit(int ruleId, UpdateRateLimitRuleRequest request, CancellationToken ct) =>
        await service.UpdateRateLimitAsync(ruleId, request, ct) is { } result ? Ok(result) : NotFound();

    [HttpDelete("rate-limits/{ruleId:int}")]
    public async Task<IActionResult> DeleteRateLimit(int ruleId, CancellationToken ct) =>
        await service.DeleteRateLimitAsync(ruleId, ct) ? NoContent() : NotFound();
}

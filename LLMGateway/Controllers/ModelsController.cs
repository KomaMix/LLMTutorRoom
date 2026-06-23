using LLMGateway.Data;
using LLMGateway.Data.Models;
using LLMGateway.DTOs.Models;
using LLMGateway.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LLMGateway.Controllers
{
    [ApiController]
    [Route("api/models")]
    public class ModelsController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly ChatClientFactory _chatClientFactory;

        public ModelsController(AppDbContext dbContext, ChatClientFactory chatClientFactory)
        {
            _dbContext = dbContext;
            _chatClientFactory = chatClientFactory;
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyCollection<string>>> GetModels(CancellationToken cancellationToken)
        {
            var models = await _dbContext.Models
                .AsNoTracking()
                .Where(m => m.Deployments.Any(d => d.IsEnabled))
                .OrderBy(m => m.Key)
                .Select(m => m.Key)
                .ToListAsync(cancellationToken);

            return Ok(models);
        }

        [HttpPost]
        public async Task<ActionResult<ModelResponse>> CreateModel(
            [FromBody] CreateModelRequest request,
            CancellationToken cancellationToken)
        {
            if (await _dbContext.Models.AnyAsync(m => m.Key == request.Key, cancellationToken))
                return Conflict($"Model '{request.Key}' already exists.");

            var model = new Model
            {
                Key = request.Key,
                DisplayName = request.DisplayName,
                Description = request.Description
            };
            _dbContext.Models.Add(model);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return CreatedAtAction(nameof(GetModel), new { modelKey = model.Key }, ToResponse(model));
        }

        [HttpGet("{modelKey}")]
        public async Task<ActionResult<ModelResponse>> GetModel(
            [FromRoute] string modelKey,
            CancellationToken cancellationToken)
        {
            var model = await GetModelWithDeploymentsAsync(modelKey, cancellationToken);
            return model is null ? NotFound() : Ok(ToResponse(model));
        }

        [HttpPost("{modelKey}/deployments")]
        public async Task<ActionResult<ModelDeploymentResponse>> CreateDeployment(
            [FromRoute] string modelKey,
            [FromBody] CreateModelDeploymentRequest request,
            CancellationToken cancellationToken)
        {
            var model = await _dbContext.Models.SingleOrDefaultAsync(m => m.Key == modelKey, cancellationToken);
            if (model is null)
                return NotFound();

            var providerType = request.ProviderType!.Value;
            if (!_chatClientFactory.SupportsProvider(providerType))
                return BadRequest($"Unsupported provider type '{providerType}'.");

            var deployment = new ModelDeployment
            {
                ModelId = model.Id,
                ProviderType = providerType,
                Endpoint = request.Endpoint,
                ApiKey = request.ApiKey,
                ProviderModelId = request.ProviderModelId,
                IsEnabled = request.IsEnabled,
                Priority = request.Priority,
                MaxConcurrentRequests = request.MaxConcurrentRequests
            };
            _dbContext.ModelDeployments.Add(deployment);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return CreatedAtAction(nameof(GetModel), new { modelKey }, ToResponse(deployment));
        }

        [HttpPut("deployments/{deploymentId:int}")]
        public async Task<ActionResult<ModelDeploymentResponse>> UpdateDeployment(
            [FromRoute] int deploymentId,
            [FromBody] UpdateModelDeploymentRequest request,
            CancellationToken cancellationToken)
        {
            var deployment = await _dbContext.ModelDeployments
                .Include(d => d.RateLimitRules)
                .SingleOrDefaultAsync(d => d.Id == deploymentId, cancellationToken);
            if (deployment is null)
                return NotFound();

            var providerType = request.ProviderType!.Value;
            if (!_chatClientFactory.SupportsProvider(providerType))
                return BadRequest($"Unsupported provider type '{providerType}'.");

            deployment.ProviderType = providerType;
            deployment.Endpoint = request.Endpoint;
            deployment.ApiKey = request.ApiKey;
            deployment.ProviderModelId = request.ProviderModelId;
            deployment.IsEnabled = request.IsEnabled;
            deployment.Priority = request.Priority;
            deployment.MaxConcurrentRequests = request.MaxConcurrentRequests;
            deployment.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);
            return Ok(ToResponse(deployment));
        }

        [HttpPost("deployments/{deploymentId:int}/rate-limits")]
        public async Task<ActionResult<RateLimitRuleResponse>> CreateRateLimitRule(
            [FromRoute] int deploymentId,
            [FromBody] CreateRateLimitRuleRequest request,
            CancellationToken cancellationToken)
        {
            var deploymentExists = await _dbContext.ModelDeployments
                .AnyAsync(d => d.Id == deploymentId, cancellationToken);
            if (!deploymentExists)
                return NotFound();

            var rule = new ModelRateLimitRule
            {
                ModelDeploymentId = deploymentId,
                WindowSeconds = request.WindowSeconds,
                MaxRequests = request.MaxRequests
            };
            _dbContext.ModelRateLimitRules.Add(rule);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(ToResponse(rule));
        }

        [HttpDelete("rate-limits/{rateLimitRuleId:int}")]
        public async Task<IActionResult> DeleteRateLimitRule(
            [FromRoute] int rateLimitRuleId,
            CancellationToken cancellationToken)
        {
            var deletedRows = await _dbContext.ModelRateLimitRules
                .Where(r => r.Id == rateLimitRuleId)
                .ExecuteDeleteAsync(cancellationToken);

            return deletedRows == 0 ? NotFound() : NoContent();
        }

        private Task<Model?> GetModelWithDeploymentsAsync(string modelKey, CancellationToken cancellationToken)
        {
            return _dbContext.Models
                .AsNoTracking()
                .Include(m => m.Deployments)
                    .ThenInclude(d => d.RateLimitRules)
                .SingleOrDefaultAsync(m => m.Key == modelKey, cancellationToken);
        }

        private static ModelResponse ToResponse(Model model)
        {
            return new ModelResponse
            {
                Key = model.Key,
                DisplayName = model.DisplayName,
                Description = model.Description,
                Deployments = model.Deployments.Select(ToResponse).ToList()
            };
        }

        private static ModelDeploymentResponse ToResponse(ModelDeployment deployment)
        {
            return new ModelDeploymentResponse
            {
                Id = deployment.Id,
                ProviderType = deployment.ProviderType,
                Endpoint = deployment.Endpoint,
                ProviderModelId = deployment.ProviderModelId,
                IsEnabled = deployment.IsEnabled,
                Priority = deployment.Priority,
                MaxConcurrentRequests = deployment.MaxConcurrentRequests,
                RateLimitRules = deployment.RateLimitRules.Select(ToResponse).ToList()
            };
        }

        private static RateLimitRuleResponse ToResponse(ModelRateLimitRule rule)
        {
            return new RateLimitRuleResponse
            {
                Id = rule.Id,
                WindowSeconds = rule.WindowSeconds,
                MaxRequests = rule.MaxRequests
            };
        }
    }
}

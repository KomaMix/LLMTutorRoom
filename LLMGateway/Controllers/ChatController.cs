using LLMGateway.Data;
using LLMGateway.Data.Models;
using LLMGateway.DTOs;
using LLMGateway.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace LLMGateway.Controllers
{
    [ApiController]
    [Route("api/chat")]
    public class ChatController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly ChatClientFactory _chatClientFactory;
        private readonly RateLimitService _rateLimitService;

        public ChatController(
            AppDbContext dbContext,
            ChatClientFactory chatClientFactory,
            RateLimitService rateLimitService)
        {
            _dbContext = dbContext;
            _chatClientFactory = chatClientFactory;
            _rateLimitService = rateLimitService;
        }

        [HttpPost]
        public async Task<ActionResult<LLMGateway.DTOs.ChatResponse>> Chat(
            [FromBody] ChatRequest request,
            CancellationToken cancellationToken)
        {
            var deployment = await TryAcquireDeploymentAsync(request.Model, cancellationToken);
            if (deployment is null)
                return Conflict($"No enabled deployment for model '{request.Model}' is available.");

            try
            {
                if (!await _rateLimitService.TryConsumeAsync(deployment.RateLimitRules, cancellationToken))
                    return StatusCode(StatusCodes.Status429TooManyRequests, "Rate limit exceeded.");

                var client = _chatClientFactory.CreateClient(deployment);
                var response = await client.GetResponseAsync(
                    request.Messages.Select(ToChatMessage).ToList(),
                    new ChatOptions { Temperature = request.Temperature },
                    cancellationToken);

                return Ok(new LLMGateway.DTOs.ChatResponse
                {
                    Model = request.Model,
                    DeploymentId = deployment.Id,
                    ProviderType = deployment.ProviderType,
                    ProviderModelId = deployment.ProviderModelId,
                    Text = response.Text
                });
            }
            finally
            {
                await ReleaseDeploymentAsync(deployment.Id, CancellationToken.None);
            }
        }

        private async Task<ModelDeployment?> TryAcquireDeploymentAsync(string modelKey, CancellationToken cancellationToken)
        {
            while (true)
            {
                var deployment = await _dbContext.ModelDeployments
                    .AsNoTracking()
                    .Include(d => d.RateLimitRules)
                    .Where(d => d.Model.Key == modelKey
                        && d.IsEnabled
                        && d.CurrentRequestCount < d.MaxConcurrentRequests)
                    .OrderBy(d => d.Priority)
                    .ThenBy(d => d.Id)
                    .FirstOrDefaultAsync(cancellationToken);

                if (deployment is null)
                    return null;

                var updatedRows = await _dbContext.ModelDeployments
                    .Where(d => d.Id == deployment.Id
                        && d.IsEnabled
                        && d.CurrentRequestCount < d.MaxConcurrentRequests)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(d => d.CurrentRequestCount, d => d.CurrentRequestCount + 1)
                            .SetProperty(d => d.UpdatedAt, DateTime.UtcNow),
                        cancellationToken);

                if (updatedRows == 1)
                    return deployment;
            }
        }

        private Task ReleaseDeploymentAsync(int deploymentId, CancellationToken cancellationToken)
        {
            return _dbContext.ModelDeployments
                .Where(d => d.Id == deploymentId && d.CurrentRequestCount > 0)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(d => d.CurrentRequestCount, d => d.CurrentRequestCount - 1)
                        .SetProperty(d => d.UpdatedAt, DateTime.UtcNow),
                    cancellationToken);
        }

        private static ChatMessage ToChatMessage(ChatMessageRequest message)
        {
            var role = message.Role switch
            {
                "system" => ChatRole.System,
                "assistant" => ChatRole.Assistant,
                _ => ChatRole.User
            };

            return new ChatMessage(role, message.Content);
        }
    }
}

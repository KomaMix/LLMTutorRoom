using LLMGateway.Data;
using LLMGateway.Data.Models;
using LLMGateway.DTOs.Chat;
using LLMGateway.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace LLMGateway.Services
{
    public class ChatExecutionService
    {
        private readonly AppDbContext _dbContext;
        private readonly ChatClientFactory _chatClientFactory;
        private readonly RateLimitService _rateLimitService;

        public ChatExecutionService(
            AppDbContext dbContext,
            ChatClientFactory chatClientFactory,
            RateLimitService rateLimitService)
        {
            _dbContext = dbContext;
            _chatClientFactory = chatClientFactory;
            _rateLimitService = rateLimitService;
        }

        public async Task<(ChatCompletionResponse? Response, ChatExecutionStatus Status)> ExecuteAsync(
            ChatRequest request,
            CancellationToken cancellationToken)
        {
            var deployment = await TryAcquireDeploymentAsync(request.Model, cancellationToken);
            if (deployment is null)
                return (null, ChatExecutionStatus.NoAvailableDeployment);

            try
            {
                if (!await _rateLimitService.TryConsumeAsync(deployment.RateLimitRules, cancellationToken))
                    return (null, ChatExecutionStatus.RateLimitExceeded);

                var client = _chatClientFactory.CreateClient(deployment);
                var response = await client.GetResponseAsync(
                    request.Messages.Select(ToChatMessage).ToList(),
                    new ChatOptions { Temperature = request.Temperature },
                    cancellationToken);

                return (
                    new ChatCompletionResponse
                    {
                        Model = request.Model,
                        Text = response.Text
                    },
                    ChatExecutionStatus.Completed);
            }
            finally
            {
                await ReleaseDeploymentAsync(deployment.Id, CancellationToken.None);
            }
        }

        private async Task<ModelDeployment?> TryAcquireDeploymentAsync(
            string modelKey,
            CancellationToken cancellationToken)
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

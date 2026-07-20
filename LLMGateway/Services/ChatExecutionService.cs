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
        private readonly ILogger<ChatExecutionService> _logger;

        public ChatExecutionService(
            AppDbContext dbContext,
            ChatClientFactory chatClientFactory,
            RateLimitService rateLimitService,
            ILogger<ChatExecutionService> logger)
        {
            _dbContext = dbContext;
            _chatClientFactory = chatClientFactory;
            _rateLimitService = rateLimitService;
            _logger = logger;
        }

        public async Task<(
            ChatExecutionStatus Status,
            ChatCompletionResponse? Response)> ExecuteAsync(
            ChatRequest request,
            CancellationToken cancellationToken)
        {
            var model = await _dbContext.Models
                .AsNoTracking()
                .Include(m => m.Deployments)
                .SingleOrDefaultAsync(m => m.Key == request.Model, cancellationToken);

            if (model is null)
                return (
                    ChatExecutionStatus.ModelNotFound,
                    null);

            var deployments = model.Deployments
                .Where(d => d.IsEnabled)
                .OrderBy(d => d.Priority)
                .ThenBy(d => d.Id)
                .ToList();

            if (deployments.Count == 0)
                return (
                    ChatExecutionStatus.NoAvailableDeployment,
                    null);

            var messages = request.Messages.Select(ToChatMessage).ToList();
            var sawRateLimitedDeployment = false;
            var sawProviderFailure = false;
            var sawProviderTimeout = false;

            foreach (var deployment in deployments)
            {
                try
                {
                    if (!_rateLimitService.TryConsume(
                            deployment.Id,
                            deployment.RateLimitRules))
                    {
                        sawRateLimitedDeployment = true;
                        continue;
                    }

                    var client = _chatClientFactory.CreateClient(deployment);
                    var response = await client.GetResponseAsync(
                        messages,
                        new ChatOptions { Temperature = request.Temperature },
                        cancellationToken);

                    return (
                        ChatExecutionStatus.Completed,
                        new ChatCompletionResponse
                        {
                            Model = request.Model,
                            Text = response.Text
                        });
                }
                catch (OperationCanceledException)
                {
                    if (cancellationToken.IsCancellationRequested)
                        throw;

                    sawProviderTimeout = true;
                    _logger.LogWarning(
                        "Deployment {DeploymentId} for model {ModelKey} timed out.",
                        deployment.Id,
                        request.Model);
                }
                catch (Exception ex)
                {
                    sawProviderFailure = true;
                    _logger.LogWarning(
                        ex,
                        "Deployment {DeploymentId} for model {ModelKey} failed.",
                        deployment.Id,
                        request.Model);
                }
            }

            if (sawProviderFailure || sawProviderTimeout)
            {
                var status = sawProviderFailure
                    ? ChatExecutionStatus.ProviderFailed
                    : ChatExecutionStatus.ProviderTimedOut;

                return (
                    status,
                    null);
            }

            if (sawRateLimitedDeployment)
                return (
                    ChatExecutionStatus.RateLimitExceeded,
                    null);

            return (
                ChatExecutionStatus.NoAvailableDeployment,
                null);
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

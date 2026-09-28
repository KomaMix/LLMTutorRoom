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
        private readonly IDeploymentProbe _probe;
        private readonly ILogger<ChatExecutionService> _logger;

        public ChatExecutionService(
            AppDbContext dbContext,
            ChatClientFactory chatClientFactory,
            RateLimitService rateLimitService,
            IDeploymentProbe probe,
            ILogger<ChatExecutionService> logger)
        {
            _dbContext = dbContext;
            _chatClientFactory = chatClientFactory;
            _rateLimitService = rateLimitService;
            _probe = probe;
            _logger = logger;
        }

        public async Task<(
            ChatExecutionStatus Status,
            ChatCompletionResponse? Response)> ExecuteAsync(
            int modelId,
            ChatRequest request,
            CancellationToken cancellationToken)
        {
            var model = await _dbContext.Models
                .AsNoTracking()
                .Include(m => m.Deployments).ThenInclude(d => d.RateLimitRules)
                .SingleOrDefaultAsync(m => m.Id == modelId, cancellationToken);

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
            var sawConcurrencyLimitedDeployment = false;
            var sawProviderUnavailable = false;
            var sawProviderFailure = false;
            var sawProviderTimeout = false;

            foreach (var deployment in deployments)
            {
                var shouldReleaseConcurrency = false;
                try
                {
                    var limitCheckResult = _rateLimitService.TryAcquire(
                            deployment.Id,
                            deployment.MaxConcurrentRequests,
                            deployment.RateLimitRules);

                    if (limitCheckResult == LimitCheckResult.RateLimitExceeded)
                    {
                        sawRateLimitedDeployment = true;
                        continue;
                    }

                    if (limitCheckResult == LimitCheckResult.ConcurrencyLimitExceeded)
                    {
                        sawConcurrencyLimitedDeployment = true;
                        continue;
                    }

                    shouldReleaseConcurrency = true;

                    var providerCheck = await _probe.CheckAsync(deployment, cancellationToken);

                    if (!providerCheck.IsAvailable)
                    {
                        sawProviderUnavailable = true;
                        continue;
                    }

                    using var client = _chatClientFactory.CreateClient(deployment);
                    var response = await client.GetResponseAsync(
                        messages,
                        new ChatOptions { Temperature = request.Temperature },
                        cancellationToken);

                    return (
                        ChatExecutionStatus.Completed,
                        new ChatCompletionResponse
                        {
                            ModelId = model.Id,
                            ModelDisplayName = model.DisplayName,
                            DeploymentId = deployment.Id,
                            DeploymentName = deployment.Name,
                            ProviderModelId = deployment.ProviderModelId,
                            DeploymentUpdatedAt = new DateTimeOffset(deployment.UpdatedAt, TimeSpan.Zero),
                            Text = response.Text
                        });
                }
                catch (OperationCanceledException)
                {
                    if (cancellationToken.IsCancellationRequested)
                        throw;

                    sawProviderTimeout = true;
                    _logger.LogWarning(
                        "Deployment {DeploymentId} for model {ModelId} timed out.",
                        deployment.Id,
                        modelId);
                }
                catch (Exception ex)
                {
                    sawProviderFailure = true;
                    _logger.LogWarning(
                        "Deployment {DeploymentId} for model {ModelId} failed ({ExceptionType}).",
                        deployment.Id,
                        modelId,
                        ex.GetType().Name);
                }
                finally
                {
                    if (shouldReleaseConcurrency)
                    {
                        _rateLimitService.Release(deployment.Id);
                    }
                }
            }

            var status = ChatExecutionStatus.NoAvailableDeployment;

            switch (true)
            {
                case true when sawProviderFailure:
                    status = ChatExecutionStatus.ProviderFailed;
                    break;
                case true when sawProviderTimeout:
                    status = ChatExecutionStatus.ProviderTimedOut;
                    break;
                case true when sawProviderUnavailable:
                    status = ChatExecutionStatus.ProviderUnavailable;
                    break;
                case true when sawRateLimitedDeployment:
                    status = ChatExecutionStatus.RateLimitExceeded;
                    break;
                case true when sawConcurrencyLimitedDeployment:
                    status = ChatExecutionStatus.ConcurrencyLimitExceeded;
                    break;
            }

            return (
                status,
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

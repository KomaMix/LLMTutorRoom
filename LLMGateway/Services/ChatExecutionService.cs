using LLMGateway.Data;
using LLMGateway.Data.Models;
using LLMGateway.DTOs.Chat;
using LLMGateway.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using System.Net.Http.Headers;

namespace LLMGateway.Services
{
    public class ChatExecutionService
    {
        private static readonly TimeSpan ProviderHealthCheckTimeout = TimeSpan.FromSeconds(3);

        private readonly AppDbContext _dbContext;
        private readonly ChatClientFactory _chatClientFactory;
        private readonly RateLimitService _rateLimitService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ChatExecutionService> _logger;

        public ChatExecutionService(
            AppDbContext dbContext,
            ChatClientFactory chatClientFactory,
            RateLimitService rateLimitService,
            IHttpClientFactory httpClientFactory,
            ILogger<ChatExecutionService> logger)
        {
            _dbContext = dbContext;
            _chatClientFactory = chatClientFactory;
            _rateLimitService = rateLimitService;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<(
            ChatExecutionStatus Status,
            ChatCompletionResponse? Response)> ExecuteAsync(
            string modelKey,
            ChatRequest request,
            CancellationToken cancellationToken)
        {
            var model = await _dbContext.Models
                .AsNoTracking()
                .Include(m => m.Deployments)
                .SingleOrDefaultAsync(m => m.Key == modelKey, cancellationToken);

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

                    shouldReleaseConcurrency = deployment.MaxConcurrentRequests.HasValue
                        && deployment.MaxConcurrentRequests.Value > 0;

                    var isProviderAvailable = await IsProviderAvailableAsync(
                        deployment,
                        modelKey,
                        cancellationToken);

                    if (!isProviderAvailable)
                    {
                        sawProviderUnavailable = true;
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
                            Model = modelKey,
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
                        modelKey);
                }
                catch (Exception ex)
                {
                    sawProviderFailure = true;
                    _logger.LogWarning(
                        ex,
                        "Deployment {DeploymentId} for model {ModelKey} failed.",
                        deployment.Id,
                        modelKey);
                }
                finally
                {
                    if (shouldReleaseConcurrency)
                    {
                        _rateLimitService.Release(
                            deployment.Id,
                            deployment.MaxConcurrentRequests);
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

        private async Task<bool> IsProviderAvailableAsync(
            ModelDeployment deployment,
            string modelKey,
            CancellationToken cancellationToken)
        {
            var httpClient = _httpClientFactory.CreateClient();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(ProviderHealthCheckTimeout);

            try
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    GetModelsEndpoint(deployment));

                if (!string.IsNullOrWhiteSpace(deployment.ApiKey))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", deployment.ApiKey);

                using var response = await httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeoutCts.Token);

                if (response.IsSuccessStatusCode)
                    return true;

                _logger.LogWarning(
                    "Deployment {DeploymentId} for model {ModelKey} health check returned {StatusCode}.",
                    deployment.Id,
                    modelKey,
                    response.StatusCode);

                return false;
            }
            catch (OperationCanceledException)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw;

                _logger.LogWarning(
                    "Deployment {DeploymentId} for model {ModelKey} health check timed out.",
                    deployment.Id,
                    modelKey);

                return false;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Deployment {DeploymentId} for model {ModelKey} health check failed.",
                    deployment.Id,
                    modelKey);

                return false;
            }
            catch (UriFormatException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Deployment {DeploymentId} for model {ModelKey} has invalid endpoint.",
                    deployment.Id,
                    modelKey);

                return false;
            }
        }

        private static Uri GetModelsEndpoint(ModelDeployment deployment)
        {
            var endpoint = deployment.Endpoint.TrimEnd('/');
            return new Uri($"{endpoint}/models");
        }
    }
}

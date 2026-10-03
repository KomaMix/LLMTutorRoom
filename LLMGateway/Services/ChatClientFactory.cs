using LLMGateway.Data.Models;
using Microsoft.Extensions.AI;
using OpenAI;
using Shared.Observability;
using System.ClientModel;

namespace LLMGateway.Services;

public class ChatClientFactory
{
    private const string DefaultApiKey = "openai-compatible";
    private readonly ILoggerFactory _loggerFactory;

    public ChatClientFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public virtual IChatClient CreateClient(ModelDeployment deployment)
    {
        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri(deployment.Endpoint)
        };
        var apiKey = string.IsNullOrWhiteSpace(deployment.ApiKey)
            ? DefaultApiKey
            : deployment.ApiKey;

        var openAiClient = new OpenAIClient(new ApiKeyCredential(apiKey), options);
        var chatClient = openAiClient
            .GetChatClient(deployment.ProviderModelId)
            .AsIChatClient();

        return new ChatClientBuilder(chatClient)
            .UseOpenTelemetry(
                _loggerFactory,
                ProjectTelemetry.LlmTelemetryName,
                telemetry => telemetry.EnableSensitiveData = false)
            .Build();
    }
}

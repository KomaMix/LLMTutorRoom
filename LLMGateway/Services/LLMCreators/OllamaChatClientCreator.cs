using LLMGateway.Data.Models;
using LLMGateway.Enums;
using LLMGateway.Interfaces;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;

namespace LLMGateway.Services.LLMCreators
{
    public class OllamaChatClientCreator : IChatClientCreator
    {
        public ModelProviderType ProviderType => ModelProviderType.Ollama;

        public IChatClient CreateClient(ModelDeployment deployment)
        {
            var endpoint = deployment.Endpoint.TrimEnd('/');
            if (!endpoint.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
                endpoint += "/v1";
            var options = new OpenAIClientOptions
            {
                Endpoint = new Uri(endpoint)
            };

            var client = new OpenAIClient(new ApiKeyCredential(deployment.ApiKey ?? "ollama"), options);
            return client.GetChatClient(deployment.ProviderModelId).AsIChatClient();
        }
    }
}

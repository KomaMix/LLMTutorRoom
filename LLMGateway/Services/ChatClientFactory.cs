using LLMGateway.Data.Models;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;

namespace LLMGateway.Services
{
    public class ChatClientFactory
    {
        private const string DefaultApiKey = "openai-compatible";

        public virtual IChatClient CreateClient(ModelDeployment deployment)
        {
            var options = new OpenAIClientOptions
            {
                Endpoint = new Uri(deployment.Endpoint)
            };
            var apiKey = string.IsNullOrWhiteSpace(deployment.ApiKey)
                ? DefaultApiKey
                : deployment.ApiKey;

            var client = new OpenAIClient(new ApiKeyCredential(apiKey), options);
            return client.GetChatClient(deployment.ProviderModelId).AsIChatClient();
        }
    }
}

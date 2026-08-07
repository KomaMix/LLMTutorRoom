using LLMGateway.Data.Models;
using LLMGateway.Enums;
using LLMGateway.Interfaces;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;

namespace LLMGateway.Services.LLMCreators
{
    public class OpenAiCompatibleChatClientCreator : IChatClientCreator
    {
        public ModelProviderType ProviderType => ModelProviderType.OpenAiCompatible;

        public IChatClient CreateClient(ModelDeployment deployment)
        {
            if (string.IsNullOrEmpty(deployment.ApiKey))
                throw new InvalidOperationException("API key is required.");

            var options = new OpenAIClientOptions
            {
                Endpoint = new Uri(deployment.Endpoint)
            };
            var client = new OpenAIClient(new ApiKeyCredential(deployment.ApiKey), options);
            return client.GetChatClient(deployment.ProviderModelId).AsIChatClient();
        }
    }
}

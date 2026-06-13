using LLMGateway.Data.Models;
using LLMGateway.Interfaces;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;

namespace LLMGateway.Services.LLMCreators
{
    public class OpenAiCompatibleChatClientCreator : IChatClientCreator
    {
        public string Type => "OpenAiCompatible";

        public IChatClient CreateClient(LLMModelInfo model)
        {
            if (string.IsNullOrEmpty(model.ApiKey))
                throw new InvalidOperationException($"API key is required");

            var options = new OpenAIClientOptions
            {
                Endpoint = new Uri(model.Endpoint),
                NetworkTimeout = TimeSpan.FromMinutes(3)
            };
            var client = new OpenAIClient(new ApiKeyCredential(model.ApiKey), options);
            return client.GetChatClient(model.ModelId).AsIChatClient();
        }
    }
}

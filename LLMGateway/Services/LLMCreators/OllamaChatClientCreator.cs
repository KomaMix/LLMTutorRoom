using LLMGateway.Data.Models;
using LLMGateway.Interfaces;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;

namespace LLMGateway.Services.LLMCreators
{
    public class OllamaChatClientCreator : IChatClientCreator
    {
        public string Type => "Ollama";

        public IChatClient CreateClient(LLMModelInfo model)
        {
            // Ollama предоставляет OpenAI-совместимый эндпоинт на /v1
            var endpoint = model.Endpoint.TrimEnd('/') + "/v1";
            var options = new OpenAIClientOptions
            {
                Endpoint = new Uri(endpoint),
                NetworkTimeout = TimeSpan.FromMinutes(3)
            };

            var client = new OpenAIClient(new ApiKeyCredential(string.Empty), options);
            return client.GetChatClient(model.ModelId).AsIChatClient();
        }
    }
}

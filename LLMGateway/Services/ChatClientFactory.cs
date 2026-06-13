using LLMGateway.Data.Models;
using LLMGateway.Interfaces;
using Microsoft.Extensions.AI;

namespace LLMGateway.Services
{
    public class ChatClientFactory
    {
        private readonly Dictionary<string, IChatClientCreator> _creators;

        public ChatClientFactory(IEnumerable<IChatClientCreator> creators)
        {
            _creators = creators.ToDictionary(c => c.Type, c => c);
        }

        public IChatClient CreateClient(LLMModelInfo model)
        {
            if (!_creators.TryGetValue(model.Type, out var creator))
                throw new NotSupportedException($"Unsupported model type: {model.Type}");

            return creator.CreateClient(model);
        }
    }
}

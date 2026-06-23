using LLMGateway.Data.Models;
using LLMGateway.Interfaces;
using Microsoft.Extensions.AI;

namespace LLMGateway.Services
{
    public class ChatClientFactory
    {
        private readonly Dictionary<ModelProviderType, IChatClientCreator> _creators;

        public ChatClientFactory(IEnumerable<IChatClientCreator> creators)
        {
            _creators = creators.ToDictionary(c => c.ProviderType, c => c);
        }

        public IChatClient CreateClient(ModelDeployment deployment)
        {
            if (!_creators.TryGetValue(deployment.ProviderType, out var creator))
                throw new NotSupportedException($"Unsupported provider type: {deployment.ProviderType}");

            return creator.CreateClient(deployment);
        }

        public bool SupportsProvider(ModelProviderType providerType)
        {
            return _creators.ContainsKey(providerType);
        }
    }
}

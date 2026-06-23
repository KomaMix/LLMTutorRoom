using LLMGateway.Data.Models;
using Microsoft.Extensions.AI;

namespace LLMGateway.Interfaces
{
    public interface IChatClientCreator
    {
        ModelProviderType ProviderType { get; }
        IChatClient CreateClient(ModelDeployment deployment);
    }
}

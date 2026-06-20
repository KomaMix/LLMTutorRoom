using LLMGateway.Data.Models;
using Microsoft.Extensions.AI;

namespace LLMGateway.Interfaces
{
    public interface IChatClientCreator
    {
        string Type { get; }
        IChatClient CreateClient(ModelDeployment deployment);
    }
}

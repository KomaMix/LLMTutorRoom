using LLMGateway.Data.Models;
using LLMGateway.DTOs.Models;

namespace LLMGateway.Services;

public interface IDeploymentProbe
{
    Task<DeploymentCheckResponse> CheckAsync(ModelDeployment deployment, CancellationToken ct);
}

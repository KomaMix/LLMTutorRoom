using LLMGateway.DTOs.Models;
using LLMGateway.Services;
using Microsoft.AspNetCore.Mvc;

namespace LLMGateway.Controllers;

[ApiController]
[Route("internal/models")]
public sealed class ModelCatalogController(IModelManagementService service) : ControllerBase
{
    [HttpGet("catalog")]
    public async Task<ActionResult<List<ModelCatalogItemResponse>>> GetCatalog(CancellationToken ct) =>
        Ok(await service.GetCatalogAsync(ct));
}

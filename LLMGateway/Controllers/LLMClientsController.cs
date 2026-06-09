using LLMGateway.Data;
using LLMGateway.Data.Models;
using LLMGateway.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LLMGateway.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LLMClientsController : ControllerBase
    {
        private readonly AppDbContext _dbContext;

        public LLMClientsController(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<LLMModelInfo>>> GetModels([FromQuery] bool onlyActive = true)
        {
            var query = _dbContext.Models.AsQueryable();
            if (onlyActive)
                query = query.Where(m => m.IsActive);

            var models = await query.ToListAsync();
            return Ok(models);
        }

        [HttpPost]
        public async Task<ActionResult<LLMModelInfo>> CreateModel([FromBody] CreateModelRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var model = new LLMModelInfo
            {
                Name = request.Name,
                Type = request.Type,
                Endpoint = request.Endpoint,
                ApiKey = request.ApiKey,
                ModelId = request.ModelId,
                RateLimitPermit = request.RateLimitPermit,
                RateLimitWindowSeconds = request.RateLimitWindowSeconds,
                IsActive = request.IsActive,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.Models.Add(model);
            await _dbContext.SaveChangesAsync();

            return CreatedAtAction(nameof(GetModels), new { id = model.Id }, model);
        }
    }
}

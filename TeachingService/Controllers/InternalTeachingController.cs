using Microsoft.AspNetCore.Mvc;
using TeachingService.Contracts.Models;
using TeachingService.Services;

namespace TeachingService.Controllers
{
    [ApiController]
    [Route("internal/teaching")]
    public sealed class InternalTeachingController : ControllerBase
    {
        private readonly TeachingCatalogService _teachingCatalogService;

        public InternalTeachingController(TeachingCatalogService teachingCatalogService)
        {
            _teachingCatalogService = teachingCatalogService;
        }

        [HttpGet("tests")]
        public async Task<ActionResult<List<CourseTestDto>>> GetTests(
            [FromQuery] bool publishedOnly,
            [FromQuery] bool includeHidden,
            CancellationToken cancellationToken)
        {
            return Ok(await _teachingCatalogService.GetAllTestsAsync(
                publishedOnly,
                includeHidden,
                cancellationToken));
        }

        [HttpGet("tests/{testId}")]
        public async Task<ActionResult<CourseTestDto>> GetTest(
            Guid testId,
            [FromQuery] bool includeHidden,
            [FromQuery] int? versionNumber,
            CancellationToken cancellationToken)
        {
            var test = await _teachingCatalogService.GetTestAsync(
                testId,
                versionNumber,
                includeHidden,
                cancellationToken);

            return test is null
                ? NotFound()
                : Ok(test);
        }
    }
}

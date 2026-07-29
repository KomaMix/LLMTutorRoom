using LLMTutorRoom.DTOs;
using LLMTutorRoom.Models;
using LLMTutorRoom.Services;
using Microsoft.AspNetCore.Mvc;

namespace LLMTutorRoom.Controllers
{
    [ApiController]
    [Route("api/classroom")]
    public class ClassroomController : ControllerBase
    {
        private readonly ClassroomService _classroomService;

        public ClassroomController(ClassroomService classroomService)
        {
            _classroomService = classroomService;
        }

        [HttpGet("overview")]
        public ActionResult<ClassroomOverview> GetOverview()
        {
            return Ok(_classroomService.GetOverview());
        }

        [HttpPost("reviews")]
        public async Task<ActionResult<SubmissionReview>> CreateReview(
            [FromBody] ReviewRequest request,
            CancellationToken cancellationToken)
        {
            var review = await _classroomService.CreateReviewAsync(request, cancellationToken);
            return review is null
                ? NotFound()
                : Ok(review);
        }
    }
}

using LLMTutorRoom.DTOs;
using LLMTutorRoom.Models;
using LLMTutorRoom.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LLMTutorRoom.Controllers
{
    [ApiController]
    [Authorize]
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
            if (User.IsInRole("Teacher"))
                return Ok(_classroomService.GetTeacherOverview());

            if (User.IsInRole("Student"))
                return Ok(_classroomService.GetStudentOverview(GetDisplayName()));

            return Forbid();
        }

        [Authorize(Roles = "Student")]
        [HttpPost("reviews")]
        public async Task<ActionResult<SubmissionReview>> CreateReview(
            [FromBody] ReviewRequest request,
            CancellationToken cancellationToken)
        {
            var review = await _classroomService.CreateReviewAsync(
                request,
                GetDisplayName(),
                cancellationToken);

            return review is null
                ? NotFound()
                : Ok(review);
        }

        private string GetDisplayName()
        {
            return User.Identity?.Name ?? "Студент";
        }
    }
}

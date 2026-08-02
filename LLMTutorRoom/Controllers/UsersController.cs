using LLMTutorRoom.DTOs;
using LLMTutorRoom.Models;
using LLMTutorRoom.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LLMTutorRoom.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly AuthService _authService;

        public UsersController(AuthService authService)
        {
            _authService = authService;
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("teachers")]
        public async Task<ActionResult<IReadOnlyCollection<AuthUserResponse>>> GetTeachers(
            CancellationToken cancellationToken)
        {
            var teachers = await _authService.GetTeachersAsync(cancellationToken);
            return Ok(teachers.Select(ToResponse).ToList());
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("teachers")]
        public async Task<ActionResult<AuthUserResponse>> CreateTeacher(
            [FromBody] CreateTeacherRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.UserName)
                || string.IsNullOrWhiteSpace(request.Password)
                || string.IsNullOrWhiteSpace(request.DisplayName))
            {
                return BadRequest();
            }

            var userName = AuthService.NormalizeUserName(request.UserName);

            var teacher = await _authService.CreateTeacherAsync(
                userName,
                request.Password,
                request.DisplayName,
                cancellationToken);

            return teacher is null
                ? Conflict()
                : Ok(ToResponse(teacher));
        }

        private static AuthUserResponse ToResponse(UserAccount user)
        {
            return new AuthUserResponse
            {
                UserName = user.UserName,
                DisplayName = user.DisplayName,
                Role = user.Role.ToString()
            };
        }
    }
}

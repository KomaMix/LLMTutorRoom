using LLMTutorRoom.DTOs;
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
        public ActionResult<IReadOnlyCollection<AuthUserResponse>> GetTeachers()
        {
            lock (_authService.UsersSyncRoot)
            {
                return Ok(_authService.Users
                    .Where(user => user.Role == "Teacher")
                    .OrderBy(user => user.DisplayName)
                    .Select(ToResponse)
                    .ToList());
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("teachers")]
        public ActionResult<AuthUserResponse> CreateTeacher([FromBody] CreateTeacherRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UserName)
                || string.IsNullOrWhiteSpace(request.Password)
                || string.IsNullOrWhiteSpace(request.DisplayName))
            {
                return BadRequest();
            }

            var userName = AuthService.NormalizeUserName(request.UserName);

            lock (_authService.UsersSyncRoot)
            {
                var userExists = _authService.Users.Any(user => string.Equals(
                    user.UserName,
                    userName,
                    StringComparison.OrdinalIgnoreCase));

                if (userExists)
                    return Conflict();

                var teacher = new UserAccount
                {
                    UserName = userName,
                    Password = request.Password,
                    Role = "Teacher",
                    DisplayName = request.DisplayName.Trim()
                };

                _authService.Users.Add(teacher);
                return Ok(ToResponse(teacher));
            }
        }

        private static AuthUserResponse ToResponse(UserAccount user)
        {
            return new AuthUserResponse
            {
                UserName = user.UserName,
                DisplayName = user.DisplayName,
                Role = user.Role
            };
        }
    }
}

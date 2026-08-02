using LLMTutorRoom.DTOs;
using LLMTutorRoom.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LLMTutorRoom.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;

        public AuthController(AuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public ActionResult<LoginResponse> Login([FromBody] LoginRequest request)
        {
            var user = _authService.ValidateCredentials(request.UserName, request.Password);
            if (user is null)
                return Unauthorized();

            return Ok(new LoginResponse
            {
                AccessToken = _authService.CreateAccessToken(user),
                User = ToResponse(user)
            });
        }

        [HttpPost("logout")]
        public IActionResult Logout()
        {
            return NoContent();
        }

        [Authorize]
        [HttpGet("me")]
        public ActionResult<AuthUserResponse> Me()
        {
            return Ok(new AuthUserResponse
            {
                UserName = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
                DisplayName = User.Identity?.Name ?? string.Empty,
                Role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty
            });
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

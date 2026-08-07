using System.Security.Claims;
using AuthService.DTOs;
using AuthService.Mappers;
using AuthService.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Shared.Auth;

namespace AuthService.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public sealed class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly JwtTokenFactory _jwtTokenFactory;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            JwtTokenFactory jwtTokenFactory)
        {
            _userManager = userManager;
            _jwtTokenFactory = jwtTokenFactory;
        }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login(
            [FromBody] LoginRequest request)
        {
            var userName = request.UserName?.Trim() ?? string.Empty;
            var password = request.Password ?? string.Empty;
            var user = await _userManager.FindByNameAsync(userName);

            if (user is null)
                return Unauthorized(new
                {
                    code = "user-not-found",
                    message = "Пользователь не найден."
                });

            if (!await _userManager.CheckPasswordAsync(user, password))
                return Unauthorized(new
                {
                    code = "invalid-password",
                    message = "Неверный пароль."
                });

            var roles = (await _userManager.GetRolesAsync(user)).ToList();

            return Ok(new LoginResponse
            {
                AccessToken = _jwtTokenFactory.CreateAccessToken(
                    user.Id,
                    user.UserName ?? string.Empty,
                    user.DisplayName,
                    roles),
                User = user.ToAuthUserResponse(roles)
            });
        }

        [Authorize]
        [HttpGet("me")]
        public ActionResult<AuthUserResponse> Me()
        {
            return Ok(new AuthUserResponse
            {
                Id = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
                UserName = User.FindFirstValue("preferred_username") ?? string.Empty,
                DisplayName = User.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
                Role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty
            });
        }

    }
}

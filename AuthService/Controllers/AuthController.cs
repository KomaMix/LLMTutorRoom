using System.Security.Claims;
using AuthService.DTOs;
using AuthService.Interfaces;
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
        private readonly IUserAccountService _userAccountService;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            JwtTokenFactory jwtTokenFactory,
            IUserAccountService userAccountService)
        {
            _userManager = userManager;
            _jwtTokenFactory = jwtTokenFactory;
            _userAccountService = userAccountService;
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login(
            [FromBody] LoginRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var email = request.Email.Trim();
            var user = await _userManager.FindByEmailAsync(email);

            if (user is null)
                return Unauthorized(new
                {
                    code = "email-not-found",
                    message = "Пользователь с таким email не найден."
                });

            if (!await _userManager.CheckPasswordAsync(user, request.Password))
                return Unauthorized(new
                {
                    code = "invalid-password",
                    message = "Неверный пароль."
                });

            return Ok(await CreateLoginResponseAsync(user));
        }

        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<ActionResult<LoginResponse>> RegisterStudent(
            [FromBody] RegisterStudentRequest request,
            CancellationToken cancellationToken)
        {
            var student = await _userAccountService.CreateStudentAsync(
                request.UserName,
                request.Email,
                request.Password,
                cancellationToken);

            if (student is null)
            {
                return Conflict(new
                {
                    code = "account-exists",
                    message = "Имя пользователя или email уже заняты."
                });
            }

            var response = await CreateLoginResponseAsync(student);
            return CreatedAtAction(nameof(Me), response);
        }

        [Authorize]
        [HttpGet("me")]
        public ActionResult<AuthUserResponse> Me()
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var userName = User.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
            var email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
            var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(id)
                || string.IsNullOrWhiteSpace(userName)
                || string.IsNullOrWhiteSpace(email)
                || string.IsNullOrWhiteSpace(role))
            {
                return Unauthorized();
            }

            return Ok(new AuthUserResponse
            {
                Id = id,
                UserName = userName,
                Email = email,
                Role = role
            });
        }

        private async Task<LoginResponse> CreateLoginResponseAsync(ApplicationUser user)
        {
            var roles = (await _userManager.GetRolesAsync(user)).ToList();

            return new LoginResponse
            {
                AccessToken = _jwtTokenFactory.CreateAccessToken(
                    user.Id,
                    user.UserName ?? string.Empty,
                    user.Email ?? string.Empty,
                    roles),
                User = user.ToAuthUserResponse(roles)
            };
        }

    }
}

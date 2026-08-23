using AuthService.DTOs;
using AuthService.Interfaces;
using AuthService.Mappers;
using AuthService.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Controllers
{
    [ApiController]
    [Route("api/users")]
    public sealed class UsersController : ControllerBase
    {
        private readonly IUserAccountService _userAccountService;
        private readonly UserManager<ApplicationUser> _userManager;

        public UsersController(
            IUserAccountService userAccountService,
            UserManager<ApplicationUser> userManager)
        {
            _userAccountService = userAccountService;
            _userManager = userManager;
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("teachers")]
        public async Task<ActionResult<List<AuthUserResponse>>> GetTeachers(
            CancellationToken cancellationToken)
        {
            var teachers = await _userAccountService.GetTeachersAsync(cancellationToken);
            var responses = new List<AuthUserResponse>();
            foreach (var teacher in teachers)
            {
                var roles = (await _userManager.GetRolesAsync(teacher)).ToList();
                responses.Add(teacher.ToAuthUserResponse(roles));
            }

            return Ok(responses);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("teachers")]
        public async Task<ActionResult<AuthUserResponse>> CreateTeacher(
            [FromBody] CreateTeacherRequest request,
            CancellationToken cancellationToken)
        {
            var teacher = await _userAccountService.CreateTeacherAsync(
                request.UserName,
                request.Email,
                request.Password,
                cancellationToken);

            return teacher is null
                ? Conflict()
                : Ok(teacher.ToAuthUserResponse(
                    (await _userManager.GetRolesAsync(teacher)).ToList()));
        }
    }
}

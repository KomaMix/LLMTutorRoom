using AuthService.Interfaces;
using AuthService.Models;
using Microsoft.AspNetCore.Identity;

namespace AuthService.Services
{
    public sealed class UserAccountService : IUserAccountService
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public UserAccountService(
            UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<List<ApplicationUser>> GetTeachersAsync(
            CancellationToken cancellationToken)
        {
            var teachers = await _userManager.GetUsersInRoleAsync(UserRole.Teacher.ToString());

            return teachers
                .OrderBy(user => user.UserName)
                .ToList();
        }

        public async Task<ApplicationUser?> CreateTeacherAsync(
            string userName,
            string email,
            string password,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var trimmedUserName = userName.Trim();
            var trimmedEmail = email.Trim();
            var userExists = await _userManager.FindByNameAsync(trimmedUserName)
                ?? await _userManager.FindByEmailAsync(trimmedEmail);

            if (userExists is not null)
                return null;

            var teacher = new ApplicationUser
            {
                UserName = trimmedUserName,
                Email = trimmedEmail
            };

            var createResult = await _userManager.CreateAsync(teacher, password);
            if (IsDuplicateUser(createResult))
                return null;

            ThrowIfFailed(createResult, "Create teacher");
            cancellationToken.ThrowIfCancellationRequested();

            var addRoleResult = await _userManager.AddToRoleAsync(teacher, UserRole.Teacher.ToString());
            ThrowIfFailed(addRoleResult, "Add teacher role");

            return teacher;
        }

        private static bool IsDuplicateUser(IdentityResult result)
        {
            return result.Errors.Any(error =>
                error.Code is "DuplicateUserName" or "DuplicateEmail");
        }

        private static void ThrowIfFailed(IdentityResult result, string operation)
        {
            if (result.Succeeded)
                return;

            var errors = string.Join(
                "; ",
                result.Errors.Select(error => $"{error.Code}: {error.Description}"));

            throw new InvalidOperationException(
                $"{operation} failed. Identity errors: {errors}.");
        }
    }
}

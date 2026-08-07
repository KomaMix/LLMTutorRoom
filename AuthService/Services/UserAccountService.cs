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
                .OrderBy(user => user.DisplayName)
                .ToList();
        }

        public async Task<ApplicationUser?> CreateTeacherAsync(
            string userName,
            string password,
            string displayName,
            CancellationToken cancellationToken)
        {
            var normalizedUserName = userName?.Trim() ?? string.Empty;
            var userExists = await _userManager.FindByNameAsync(normalizedUserName);

            if (userExists is not null)
                return null;

            var teacher = new ApplicationUser
            {
                UserName = normalizedUserName,
                DisplayName = displayName.Trim()
            };

            var createResult = await _userManager.CreateAsync(teacher, password);
            ThrowIfFailed(createResult, "Create teacher");
            cancellationToken.ThrowIfCancellationRequested();

            var addRoleResult = await _userManager.AddToRoleAsync(teacher, UserRole.Teacher.ToString());
            ThrowIfFailed(addRoleResult, "Add teacher role");

            return teacher;
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

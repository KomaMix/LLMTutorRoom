using AuthService.Models;
using AuthService.Options;
using Microsoft.AspNetCore.Identity;

namespace AuthService.Services
{
    public sealed class ConfiguredUsersService
    {
        private readonly IConfiguration _configuration;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public ConfiguredUsersService(
            IConfiguration configuration,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _configuration = configuration;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task EnsureConfiguredUsersAsync(CancellationToken cancellationToken)
        {
            await EnsureRolesAsync();

            var configuredUsers = _configuration
                .GetSection("Auth:Users")
                .Get<List<ConfiguredUser>>() ?? new List<ConfiguredUser>();

            foreach (var configuredUser in configuredUsers)
            {
                var userName = configuredUser.UserName?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(userName)
                    || string.IsNullOrWhiteSpace(configuredUser.Password)
                    || string.IsNullOrWhiteSpace(configuredUser.DisplayName))
                {
                    continue;
                }

                var existingUser = await _userManager.FindByNameAsync(userName);

                if (existingUser is null)
                {
                    var user = new ApplicationUser
                    {
                        UserName = userName,
                        DisplayName = configuredUser.DisplayName.Trim()
                    };

                    var createResult = await _userManager.CreateAsync(
                        user,
                        configuredUser.Password);
                    ThrowIfFailed(createResult, "Create configured user");
                    await SetSingleRoleAsync(user, configuredUser.Role.ToString());
                }
                else
                {
                    existingUser.DisplayName = configuredUser.DisplayName.Trim();
                    var updateResult = await _userManager.UpdateAsync(existingUser);
                    ThrowIfFailed(updateResult, "Update configured user");

                    await SetSingleRoleAsync(existingUser, configuredUser.Role.ToString());
                    await ResetPasswordIfNeededAsync(
                        existingUser,
                        configuredUser.Password);
                }
            }
        }

        private async Task EnsureRolesAsync()
        {
            foreach (var role in Enum.GetNames<UserRole>())
            {
                if (await _roleManager.RoleExistsAsync(role))
                    continue;

                var createResult = await _roleManager.CreateAsync(new IdentityRole(role));
                ThrowIfFailed(createResult, "Create role");
            }
        }

        private async Task SetSingleRoleAsync(
            ApplicationUser user,
            string role)
        {
            var currentRoles = await _userManager.GetRolesAsync(user);
            var rolesToRemove = currentRoles
                .Where(currentRole => !string.Equals(
                    currentRole,
                    role,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (rolesToRemove.Count > 0)
            {
                var removeResult = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
                ThrowIfFailed(removeResult, "Remove user roles");
            }

            if (!currentRoles.Any(currentRole => string.Equals(
                    currentRole,
                    role,
                    StringComparison.OrdinalIgnoreCase)))
            {
                var addResult = await _userManager.AddToRoleAsync(user, role);
                ThrowIfFailed(addResult, "Add user role");
            }
        }

        private async Task ResetPasswordIfNeededAsync(
            ApplicationUser user,
            string password)
        {
            if (await _userManager.CheckPasswordAsync(user, password))
                return;

            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetResult = await _userManager.ResetPasswordAsync(
                user,
                resetToken,
                password);
            ThrowIfFailed(resetResult, "Reset configured user password");
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

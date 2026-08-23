using AuthService.Models;
using Microsoft.AspNetCore.Identity;

namespace AuthService.Services
{
    public sealed class ApplicationUserValidator : IUserValidator<ApplicationUser>
    {
        public Task<IdentityResult> ValidateAsync(
            UserManager<ApplicationUser> manager,
            ApplicationUser user)
        {
            ArgumentNullException.ThrowIfNull(manager);
            ArgumentNullException.ThrowIfNull(user);

            var userName = user.UserName;
            if (string.IsNullOrWhiteSpace(userName)
                || userName.Any(char.IsWhiteSpace))
            {
                return Task.FromResult(IdentityResult.Failed(new IdentityError
                {
                    Code = "InvalidUserName",
                    Description = "UserName must not be empty or contain whitespace."
                }));
            }

            return Task.FromResult(IdentityResult.Success);
        }
    }
}

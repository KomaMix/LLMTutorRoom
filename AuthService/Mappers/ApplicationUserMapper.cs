using AuthService.DTOs;
using AuthService.Models;

namespace AuthService.Mappers
{
    public static class ApplicationUserMapper
    {
        public static AuthUserResponse ToAuthUserResponse(
            this ApplicationUser user,
            List<string> roles)
        {
            return new AuthUserResponse
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                DisplayName = user.DisplayName,
                Role = roles.FirstOrDefault() ?? string.Empty
            };
        }
    }
}

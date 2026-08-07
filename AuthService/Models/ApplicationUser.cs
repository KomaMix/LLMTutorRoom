using Microsoft.AspNetCore.Identity;

namespace AuthService.Models
{
    public sealed class ApplicationUser : IdentityUser
    {
        public string DisplayName { get; set; } = string.Empty;
    }
}

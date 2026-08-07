using AuthService.Models;

namespace AuthService.Options
{
    public sealed class ConfiguredUser
    {
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }
}

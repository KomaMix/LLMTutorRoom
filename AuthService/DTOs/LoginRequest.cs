using System.ComponentModel.DataAnnotations;

namespace AuthService.DTOs
{
    public sealed class LoginRequest
    {
        [Required]
        [EmailAddress]
        [StringLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }
}

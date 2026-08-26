using System.ComponentModel.DataAnnotations;

namespace AuthService.DTOs
{
    public sealed class RegisterStudentRequest
    {
        [Required]
        [StringLength(64)]
        [RegularExpression(@"^\S+$", ErrorMessage = "UserName must not contain whitespace.")]
        public string UserName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(128, MinimumLength = 6)]
        public string Password { get; set; } = string.Empty;
    }
}

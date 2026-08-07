namespace AuthService.DTOs
{
    public sealed class LoginResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public AuthUserResponse User { get; set; } = new();
    }
}

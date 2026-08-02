using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace LLMTutorRoom.Services
{
    public sealed class AuthService
    {
        private readonly string _issuer;
        private readonly string _audience;
        private readonly string _signingKey;
        private readonly int _tokenLifetimeMinutes;

        public object UsersSyncRoot { get; } = new();
        public List<UserAccount> Users { get; }

        public AuthService(IConfiguration configuration)
        {
            _issuer = configuration["Auth:Jwt:Issuer"] ?? "LLMTutorRoom";
            _audience = configuration["Auth:Jwt:Audience"] ?? "LLMTutorRoom.Client";
            _signingKey = configuration["Auth:Jwt:SigningKey"]
                ?? throw new InvalidOperationException("Auth:Jwt:SigningKey is required.");

            _tokenLifetimeMinutes = int.TryParse(
                configuration["Auth:Jwt:TokenLifetimeMinutes"],
                out var tokenLifetimeMinutes)
                ? tokenLifetimeMinutes
                : 480;

            Users = configuration
                .GetSection("Auth:Users")
                .Get<List<UserAccount>>() ?? new List<UserAccount>();

            if (Users.All(user => user.Role != "Admin"))
                throw new InvalidOperationException("At least one admin user must be configured.");
        }

        public UserAccount? ValidateCredentials(string userName, string password)
        {
            var normalizedUserName = NormalizeUserName(userName);

            lock (UsersSyncRoot)
            {
                return Users.SingleOrDefault(user =>
                    string.Equals(user.UserName, normalizedUserName, StringComparison.OrdinalIgnoreCase)
                    && user.Password == password);
            }
        }

        public string CreateAccessToken(UserAccount user)
        {
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.UserName),
                new(ClaimTypes.NameIdentifier, user.UserName),
                new(ClaimTypes.Name, user.DisplayName),
                new(ClaimTypes.Role, user.Role)
            };

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_signingKey));
            var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: _issuer,
                audience: _audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_tokenLifetimeMinutes),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public static string NormalizeUserName(string? userName)
        {
            return userName?.Trim() ?? string.Empty;
        }
    }

    public sealed class UserAccount
    {
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }
}

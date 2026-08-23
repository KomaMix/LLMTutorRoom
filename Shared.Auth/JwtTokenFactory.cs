using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Shared.Auth
{
    public sealed class JwtTokenFactory
    {
        private readonly JwtOptions _options;

        public JwtTokenFactory(JwtOptions options)
        {
            _options = options;
        }

        public string CreateAccessToken(
            string userId,
            string userName,
            string email,
            List<string> roles)
        {
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, userId),
                new(ClaimTypes.NameIdentifier, userId),
                new("preferred_username", userName),
                new(ClaimTypes.Name, userName),
                new(ClaimTypes.Email, email)
            };
            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

            var signingKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_options.SigningKey));
            var credentials = new SigningCredentials(
                signingKey,
                SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: _options.Issuer,
                audience: _options.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_options.TokenLifetimeMinutes),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}

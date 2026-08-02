using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LLMTutorRoom.Data;
using LLMTutorRoom.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace LLMTutorRoom.Services
{
    public sealed class AuthService
    {
        private readonly TutorRoomDbContext _dbContext;
        private readonly IConfiguration _configuration;
        private readonly string _issuer;
        private readonly string _audience;
        private readonly string _signingKey;
        private readonly int _tokenLifetimeMinutes;

        public AuthService(
            TutorRoomDbContext dbContext,
            IConfiguration configuration)
        {
            _dbContext = dbContext;
            _configuration = configuration;
            _issuer = configuration["Auth:Jwt:Issuer"] ?? "LLMTutorRoom";
            _audience = configuration["Auth:Jwt:Audience"] ?? "LLMTutorRoom.Client";
            _signingKey = configuration["Auth:Jwt:SigningKey"]
                ?? throw new InvalidOperationException("Auth:Jwt:SigningKey is required.");

            _tokenLifetimeMinutes = int.TryParse(
                configuration["Auth:Jwt:TokenLifetimeMinutes"],
                out var tokenLifetimeMinutes)
                ? tokenLifetimeMinutes
                : 480;
        }

        public async Task EnsureConfiguredUsersAsync(CancellationToken cancellationToken)
        {
            var configuredUsers = _configuration
                .GetSection("Auth:Users")
                .Get<List<UserAccount>>() ?? new List<UserAccount>();

            if (configuredUsers.All(user => user.Role != UserRole.Admin))
                throw new InvalidOperationException("At least one admin user must be configured.");

            foreach (var configuredUser in configuredUsers)
            {
                var userName = NormalizeUserName(configuredUser.UserName);
                if (string.IsNullOrWhiteSpace(userName)
                    || string.IsNullOrWhiteSpace(configuredUser.Password)
                    || string.IsNullOrWhiteSpace(configuredUser.DisplayName))
                {
                    continue;
                }

                var existingUser = await _dbContext.Users
                    .SingleOrDefaultAsync(user => user.UserName.ToLower() == userName.ToLower(), cancellationToken);

                if (existingUser is null)
                {
                    _dbContext.Users.Add(new UserAccount
                    {
                        UserName = userName,
                        Password = configuredUser.Password,
                        Role = configuredUser.Role,
                        DisplayName = configuredUser.DisplayName.Trim()
                    });
                }
                else
                {
                    existingUser.Password = configuredUser.Password;
                    existingUser.Role = configuredUser.Role;
                    existingUser.DisplayName = configuredUser.DisplayName.Trim();
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task<UserAccount?> ValidateCredentialsAsync(
            string userName,
            string password,
            CancellationToken cancellationToken)
        {
            var normalizedUserName = NormalizeUserName(userName);

            return await _dbContext.Users.SingleOrDefaultAsync(user =>
                user.UserName.ToLower() == normalizedUserName.ToLower()
                && user.Password == password,
                cancellationToken);
        }

        public async Task<List<UserAccount>> GetTeachersAsync(CancellationToken cancellationToken)
        {
            return await _dbContext.Users
                .Where(user => user.Role == UserRole.Teacher)
                .OrderBy(user => user.DisplayName)
                .ToListAsync(cancellationToken);
        }

        public async Task<UserAccount?> CreateTeacherAsync(
            string userName,
            string password,
            string displayName,
            CancellationToken cancellationToken)
        {
            var normalizedUserName = NormalizeUserName(userName);
            var userExists = await _dbContext.Users.AnyAsync(user =>
                user.UserName.ToLower() == normalizedUserName.ToLower(),
                cancellationToken);

            if (userExists)
                return null;

            var teacher = new UserAccount
            {
                UserName = normalizedUserName,
                Password = password,
                Role = UserRole.Teacher,
                DisplayName = displayName.Trim()
            };

            _dbContext.Users.Add(teacher);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return teacher;
        }

        public string CreateAccessToken(UserAccount user)
        {
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.UserName),
                new(ClaimTypes.NameIdentifier, user.UserName),
                new(ClaimTypes.Name, user.DisplayName),
                new(ClaimTypes.Role, user.Role.ToString())
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
}

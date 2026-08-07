using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Shared.Auth
{
    public static class JwtAuthenticationExtensions
    {
        public static IServiceCollection AddJwtAuthentication(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var jwtOptions = GetJwtOptions(configuration);

            services.Configure<JwtOptions>(
                configuration.GetSection("Auth:Jwt"));

            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = jwtOptions.Issuer,
                        ValidAudience = jwtOptions.Audience,
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                        NameClaimType = System.Security.Claims.ClaimTypes.Name,
                        RoleClaimType = System.Security.Claims.ClaimTypes.Role
                    };
                });

            services.AddAuthorization();
            return services;
        }

        public static IServiceCollection AddJwtTokenFactory(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var jwtOptions = GetJwtOptions(configuration);

            services.Configure<JwtOptions>(
                configuration.GetSection("Auth:Jwt"));
            services.AddSingleton(new JwtTokenFactory(jwtOptions));

            return services;
        }

        private static JwtOptions GetJwtOptions(IConfiguration configuration)
        {
            var jwtOptions = configuration
                .GetSection("Auth:Jwt")
                .Get<JwtOptions>() ?? new JwtOptions();

            if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey))
                throw new InvalidOperationException("Auth:Jwt:SigningKey is required.");

            if (Encoding.UTF8.GetByteCount(jwtOptions.SigningKey) < 32)
                throw new InvalidOperationException("Auth:Jwt:SigningKey must be at least 32 bytes.");

            return jwtOptions;
        }
    }
}

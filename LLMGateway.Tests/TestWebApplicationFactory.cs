using System.Net.Http.Headers;
using LLMGateway.Data.Models;
using LLMGateway.DTOs.Models;
using LLMGateway.Services;
using Microsoft.Extensions.Options;
using Shared.Auth;
using LLMGateway.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LLMGateway.Tests
{
    public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"llmgateway-tests-{Guid.NewGuid():N}";

        public HttpClient CreateAuthenticatedClient(string role)
        {
            var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
            var jwtOptions = Services.GetRequiredService<IOptions<JwtOptions>>().Value;
            var token = new JwtTokenFactory(jwtOptions).CreateAccessToken("test-user", "Test user", "test@example.test", [role]);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        private sealed class TestDeploymentProbe : IDeploymentProbe
        {
            public TestDeploymentProbe() { }
            public Task<DeploymentCheckResponse> CheckAsync(ModelDeployment deployment, CancellationToken ct) =>
                Task.FromResult(new DeploymentCheckResponse(true, "Model found.", DateTimeOffset.UtcNow, 1));
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<AppDbContext>();

                services.RemoveAll<IDeploymentProbe>();
                services.AddSingleton<IDeploymentProbe, TestDeploymentProbe>();
                services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_databaseName));
            });
        }
    }
}

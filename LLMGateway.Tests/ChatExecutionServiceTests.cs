using LLMGateway.Data;
using LLMGateway.Data.Models;
using LLMGateway.DTOs.Chat;
using LLMGateway.Interfaces;
using LLMGateway.Enums;
using LLMGateway.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using System.Runtime.CompilerServices;

namespace LLMGateway.Tests
{
    public class ChatExecutionServiceTests
    {
        [Fact]
        public async Task ExecuteAsync_WhenPrimaryDeploymentFails_UsesNextDeployment()
        {
            await using var dbContext = CreateDbContext();
            var creator = new FakeChatClientCreator();
            creator.Register("primary", new FakeChatClient(_ => throw new InvalidOperationException("provider failed")));
            creator.Register("secondary", new FakeChatClient(_ => Task.FromResult(new ChatResponse(
                new ChatMessage(ChatRole.Assistant, "secondary response")))));

            await AddModelAsync(dbContext, new[]
            {
                CreateDeployment("primary", priority: 0),
                CreateDeployment("secondary", priority: 1)
            });

            var service = CreateService(dbContext, creator);

            var result = await service.ExecuteAsync(CreateChatRequest(), CancellationToken.None);

            Assert.Equal(ChatExecutionStatus.Completed, result.Status);
            Assert.Equal("secondary response", result.Response?.Text);
        }

        [Fact]
        public async Task ExecuteAsync_WhenPrimaryDeploymentIsRateLimited_UsesNextDeployment()
        {
            await using var dbContext = CreateDbContext();
            var creator = new FakeChatClientCreator();
            creator.Register("primary", new FakeChatClient(_ => Task.FromResult(new ChatResponse(
                new ChatMessage(ChatRole.Assistant, "primary response")))));
            creator.Register("secondary", new FakeChatClient(_ => Task.FromResult(new ChatResponse(
                new ChatMessage(ChatRole.Assistant, "secondary response")))));

            await AddModelAsync(dbContext, new[]
            {
                CreateDeployment("primary", priority: 0, new[]
                {
                    new ModelRateLimitRule { Id = 1, WindowSeconds = 3600, MaxRequests = 1 }
                }),
                CreateDeployment("secondary", priority: 1)
            });

            var service = CreateService(dbContext, creator);

            var firstResult = await service.ExecuteAsync(CreateChatRequest(), CancellationToken.None);
            var secondResult = await service.ExecuteAsync(CreateChatRequest(), CancellationToken.None);

            Assert.Equal(ChatExecutionStatus.Completed, firstResult.Status);
            Assert.Equal("primary response", firstResult.Response?.Text);
            Assert.Equal(ChatExecutionStatus.Completed, secondResult.Status);
            Assert.Equal("secondary response", secondResult.Response?.Text);
        }

        [Fact]
        public async Task ExecuteAsync_WhenPrimaryDeploymentIsConcurrencyLimited_UsesNextDeployment()
        {
            await using var dbContext = CreateDbContext();
            var creator = new FakeChatClientCreator();
            var primaryStarted = new TaskCompletionSource<object?>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var releasePrimary = new TaskCompletionSource<object?>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            creator.Register("primary", new FakeChatClient(async _ =>
            {
                primaryStarted.SetResult(null);
                await releasePrimary.Task;
                return new ChatResponse(new ChatMessage(ChatRole.Assistant, "primary response"));
            }));
            creator.Register("secondary", new FakeChatClient(_ => Task.FromResult(new ChatResponse(
                new ChatMessage(ChatRole.Assistant, "secondary response")))));

            await AddModelAsync(dbContext, new[]
            {
                CreateDeployment("primary", priority: 0, maxConcurrentRequests: 1),
                CreateDeployment("secondary", priority: 1)
            });

            var service = CreateService(dbContext, creator);

            var firstTask = service.ExecuteAsync(CreateChatRequest(), CancellationToken.None);
            await primaryStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

            var secondResult = await service.ExecuteAsync(CreateChatRequest(), CancellationToken.None);

            releasePrimary.SetResult(null);
            var firstResult = await firstTask;

            Assert.Equal(ChatExecutionStatus.Completed, firstResult.Status);
            Assert.Equal("primary response", firstResult.Response?.Text);
            Assert.Equal(ChatExecutionStatus.Completed, secondResult.Status);
            Assert.Equal("secondary response", secondResult.Response?.Text);
        }

        private static ChatExecutionService CreateService(
            AppDbContext dbContext,
            FakeChatClientCreator creator)
        {
            return new ChatExecutionService(
                dbContext,
                new ChatClientFactory(new[] { creator }),
                new RateLimitService(),
                NullLogger<ChatExecutionService>.Instance);
        }

        private static AppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"chat-execution-{Guid.NewGuid():N}")
                .Options;

            return new AppDbContext(options);
        }

        private static async Task AddModelAsync(
            AppDbContext dbContext,
            IEnumerable<ModelDeployment> deployments)
        {
            dbContext.Models.Add(new Model
            {
                Key = "test-model",
                DisplayName = "Test model",
                Deployments = deployments.ToList()
            });

            await dbContext.SaveChangesAsync();
        }

        private static ModelDeployment CreateDeployment(
            string providerModelId,
            int priority,
            IEnumerable<ModelRateLimitRule>? rateLimitRules = null,
            int? maxConcurrentRequests = null)
        {
            var deployment = new ModelDeployment
            {
                ProviderType = ModelProviderType.Ollama,
                Endpoint = "http://localhost:11434",
                ProviderModelId = providerModelId,
                Priority = priority,
                IsEnabled = true,
                MaxConcurrentRequests = maxConcurrentRequests
            };

            deployment.RateLimitRules = (rateLimitRules ?? Array.Empty<ModelRateLimitRule>()).ToList();
            return deployment;
        }

        private static ChatRequest CreateChatRequest()
        {
            return new ChatRequest
            {
                Model = "test-model",
                Messages = new[]
                {
                    new ChatMessageRequest
                    {
                        Role = "user",
                        Content = "hello"
                    }
                }
            };
        }

        private sealed class FakeChatClientCreator : IChatClientCreator
        {
            private readonly Dictionary<string, IChatClient> _clients = new();

            public ModelProviderType ProviderType => ModelProviderType.Ollama;

            public IChatClient CreateClient(ModelDeployment deployment)
            {
                return _clients[deployment.ProviderModelId];
            }

            public void Register(string providerModelId, IChatClient client)
            {
                _clients.Add(providerModelId, client);
            }
        }

        private sealed class FakeChatClient : IChatClient
        {
            private readonly Func<IEnumerable<ChatMessage>, Task<ChatResponse>> _handler;

            public FakeChatClient(Func<IEnumerable<ChatMessage>, Task<ChatResponse>> handler)
            {
                _handler = handler;
            }

            public Task<ChatResponse> GetResponseAsync(
                IEnumerable<ChatMessage> messages,
                ChatOptions? options = null,
                CancellationToken cancellationToken = default)
            {
                return _handler(messages);
            }

            public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
                IEnumerable<ChatMessage> messages,
                ChatOptions? options = null,
                [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                await Task.CompletedTask;
                yield break;
            }

            public object? GetService(Type serviceType, object? serviceKey = null)
            {
                return null;
            }

            public void Dispose()
            {
            }
        }
    }
}

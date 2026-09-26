using LLMGateway.Data;
using LLMGateway.Data.Models;
using LLMGateway.DTOs.Chat;
using LLMGateway.Enums;
using LLMGateway.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;

namespace LLMGateway.Tests
{
    public class ChatExecutionServiceTests
    {
        [Fact]
        public async Task ExecuteAsync_WhenPrimaryDeploymentFails_UsesNextDeployment()
        {
            await using var dbContext = CreateDbContext();
            var chatClientFactory = new FakeChatClientFactory();
            var primaryClient = new FakeChatClient(_ => throw new InvalidOperationException("provider failed"));
            var secondaryClient = new FakeChatClient(_ => Task.FromResult(new ChatResponse(
                new ChatMessage(ChatRole.Assistant, "secondary response"))));
            chatClientFactory.Register("primary", primaryClient);
            chatClientFactory.Register("secondary", secondaryClient);

            await AddModelAsync(dbContext, new[]
            {
                CreateDeployment("primary", priority: 0),
                CreateDeployment("secondary", priority: 1)
            });

            var service = CreateService(dbContext, chatClientFactory);

            var result = await service.ExecuteAsync("test-model", CreateChatRequest(), CancellationToken.None);

            Assert.Equal(ChatExecutionStatus.Completed, result.Status);
            Assert.Equal("secondary response", result.Response?.Text);
            Assert.True(primaryClient.IsDisposed);
            Assert.True(secondaryClient.IsDisposed);
        }

        [Fact]
        public async Task ExecuteAsync_WhenPrimaryDeploymentIsRateLimited_UsesNextDeployment()
        {
            await using var dbContext = CreateDbContext();
            var chatClientFactory = new FakeChatClientFactory();
            chatClientFactory.Register("primary", new FakeChatClient(_ => Task.FromResult(new ChatResponse(
                new ChatMessage(ChatRole.Assistant, "primary response")))));
            chatClientFactory.Register("secondary", new FakeChatClient(_ => Task.FromResult(new ChatResponse(
                new ChatMessage(ChatRole.Assistant, "secondary response")))));

            await AddModelAsync(dbContext, new[]
            {
                CreateDeployment("primary", priority: 0, new[]
                {
                    new ModelRateLimitRule { Id = 1, WindowSeconds = 3600, MaxRequests = 1 }
                }),
                CreateDeployment("secondary", priority: 1)
            });

            var service = CreateService(dbContext, chatClientFactory);

            var firstResult = await service.ExecuteAsync("test-model", CreateChatRequest(), CancellationToken.None);
            var secondResult = await service.ExecuteAsync("test-model", CreateChatRequest(), CancellationToken.None);

            Assert.Equal(ChatExecutionStatus.Completed, firstResult.Status);
            Assert.Equal("primary response", firstResult.Response?.Text);
            Assert.Equal(ChatExecutionStatus.Completed, secondResult.Status);
            Assert.Equal("secondary response", secondResult.Response?.Text);
        }

        [Fact]
        public async Task ExecuteAsync_WhenPrimaryDeploymentIsConcurrencyLimited_UsesNextDeployment()
        {
            await using var dbContext = CreateDbContext();
            var chatClientFactory = new FakeChatClientFactory();
            var primaryStarted = new TaskCompletionSource<object?>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var releasePrimary = new TaskCompletionSource<object?>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            chatClientFactory.Register("primary", new FakeChatClient(async _ =>
            {
                primaryStarted.SetResult(null);
                await releasePrimary.Task;
                return new ChatResponse(new ChatMessage(ChatRole.Assistant, "primary response"));
            }));
            chatClientFactory.Register("secondary", new FakeChatClient(_ => Task.FromResult(new ChatResponse(
                new ChatMessage(ChatRole.Assistant, "secondary response")))));

            await AddModelAsync(dbContext, new[]
            {
                CreateDeployment("primary", priority: 0, maxConcurrentRequests: 1),
                CreateDeployment("secondary", priority: 1)
            });

            var service = CreateService(dbContext, chatClientFactory);

            var firstTask = service.ExecuteAsync("test-model", CreateChatRequest(), CancellationToken.None);
            await primaryStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

            var secondResult = await service.ExecuteAsync("test-model", CreateChatRequest(), CancellationToken.None);

            releasePrimary.SetResult(null);
            var firstResult = await firstTask;

            Assert.Equal(ChatExecutionStatus.Completed, firstResult.Status);
            Assert.Equal("primary response", firstResult.Response?.Text);
            Assert.Equal(ChatExecutionStatus.Completed, secondResult.Status);
            Assert.Equal("secondary response", secondResult.Response?.Text);
        }

        [Fact]
        public async Task ExecuteAsync_WhenPrimaryDeploymentIsUnavailable_UsesNextDeployment()
        {
            await using var dbContext = CreateDbContext();
            var chatClientFactory = new FakeChatClientFactory();
            chatClientFactory.Register("primary", new FakeChatClient(_ => throw new InvalidOperationException("should not be called")));
            chatClientFactory.Register("secondary", new FakeChatClient(_ => Task.FromResult(new ChatResponse(
                new ChatMessage(ChatRole.Assistant, "secondary response")))));

            await AddModelAsync(dbContext, new[]
            {
                CreateDeployment("primary", priority: 0, endpoint: "http://primary.test:11434/v1"),
                CreateDeployment("secondary", priority: 1, endpoint: "http://secondary.test:11434/v1")
            });

            var service = CreateService(
                dbContext,
                chatClientFactory,
                request => request.RequestUri?.Host == "primary.test"
                    ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                    : CreateModelsResponse("secondary"));

            var result = await service.ExecuteAsync("test-model", CreateChatRequest(), CancellationToken.None);

            Assert.Equal(ChatExecutionStatus.Completed, result.Status);
            Assert.Equal("secondary response", result.Response?.Text);
        }

        [Fact]
        public async Task ExecuteAsync_WhenProviderModelIsListed_ChecksCatalogBeforeEveryGeneration()
        {
            await using var dbContext = CreateDbContext();
            var requests = new List<string>();
            var chatClientFactory = new FakeChatClientFactory();
            chatClientFactory.Register("primary", new FakeChatClient(_ =>
            {
                requests.Add("chat");
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "response")));
            }));

            await AddModelAsync(dbContext, new[] { CreateDeployment("primary", priority: 0) });

            var service = CreateService(dbContext, chatClientFactory, request =>
            {
                requests.Add($"{request.Method} {request.RequestUri}");
                return CreateModelsResponse("other-model", "primary");
            });

            var firstResult = await service.ExecuteAsync("test-model", CreateChatRequest(), CancellationToken.None);
            var secondResult = await service.ExecuteAsync("test-model", CreateChatRequest(), CancellationToken.None);

            Assert.Equal(ChatExecutionStatus.Completed, firstResult.Status);
            Assert.Equal("response", firstResult.Response?.Text);
            Assert.Equal(ChatExecutionStatus.Completed, secondResult.Status);
            Assert.Equal("response", secondResult.Response?.Text);
            Assert.Equal(new[]
            {
                "GET http://localhost:11434/v1/models",
                "chat",
                "GET http://localhost:11434/v1/models",
                "chat"
            }, requests);
        }

        [Theory]
        [InlineData("""{"data":[{"id":"other-model"}]}""")]
        [InlineData("""{"data":[{"id":"test-model"}]}""")]
        [InlineData("""{"data":[{"id":"PRIMARY"}]}""")]
        [InlineData("""{"data":[]}""")]
        [InlineData("""{"data":null}""")]
        [InlineData("""{"data":{"id":"primary"}}""")]
        [InlineData("""{"data":[null,17,{}, {"id":42}]}""")]
        [InlineData("{}")]
        [InlineData("[]")]
        [InlineData("invalid json")]
        [InlineData("")]
        public async Task ExecuteAsync_WhenCatalogDoesNotConfirmProviderModel_DoesNotGenerate(string catalogJson)
        {
            await using var dbContext = CreateDbContext();
            var chatClientFactory = new FakeChatClientFactory();
            var chatClient = new FakeChatClient(_ => Task.FromResult(new ChatResponse(
                new ChatMessage(ChatRole.Assistant, "should not be generated"))));
            chatClientFactory.Register("primary", chatClient);

            await AddModelAsync(dbContext, new[] { CreateDeployment("primary", priority: 0) });

            var service = CreateService(dbContext, chatClientFactory, _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(catalogJson, Encoding.UTF8, "application/json")
            });

            var result = await service.ExecuteAsync("test-model", CreateChatRequest(), CancellationToken.None);

            Assert.Equal(ChatExecutionStatus.ProviderUnavailable, result.Status);
            Assert.Null(result.Response);
            Assert.Equal(0, chatClient.RequestCount);
        }

        [Fact]
        public async Task ExecuteAsync_WhenPrimaryProviderModelIsMissing_UsesNextDeployment()
        {
            await using var dbContext = CreateDbContext();
            var chatClientFactory = new FakeChatClientFactory();
            var primaryClient = new FakeChatClient(_ => Task.FromResult(new ChatResponse(
                new ChatMessage(ChatRole.Assistant, "primary response"))));
            var secondaryClient = new FakeChatClient(_ => Task.FromResult(new ChatResponse(
                new ChatMessage(ChatRole.Assistant, "secondary response"))));
            chatClientFactory.Register("primary", primaryClient);
            chatClientFactory.Register("secondary", secondaryClient);

            await AddModelAsync(dbContext, new[]
            {
                CreateDeployment("primary", priority: 0, endpoint: "http://primary.test:11434/v1"),
                CreateDeployment("secondary", priority: 1, endpoint: "http://secondary.test:11434/v1")
            });

            var service = CreateService(dbContext, chatClientFactory, request =>
                request.RequestUri?.Host == "primary.test"
                    ? CreateModelsResponse("other-model")
                    : CreateModelsResponse("secondary"));

            var result = await service.ExecuteAsync("test-model", CreateChatRequest(), CancellationToken.None);

            Assert.Equal(ChatExecutionStatus.Completed, result.Status);
            Assert.Equal("secondary response", result.Response?.Text);
            Assert.Equal(0, primaryClient.RequestCount);
            Assert.Equal(1, secondaryClient.RequestCount);
        }

        private static HttpResponseMessage CreateModelsResponse(params string[] modelIds)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    @object = "list",
                    data = modelIds.Select(id => new { id }).ToArray()
                })
            };
        }

        private static ChatExecutionService CreateService(
            AppDbContext dbContext,
            ChatClientFactory chatClientFactory,
            Func<HttpRequestMessage, HttpResponseMessage>? healthCheckHandler = null)
        {
            return new ChatExecutionService(
                dbContext,
                chatClientFactory,
                new RateLimitService(),
                new FakeHttpClientFactory(new FakeHttpMessageHandler(healthCheckHandler)),
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
            int? maxConcurrentRequests = null,
            string endpoint = "http://localhost:11434/v1")
        {
            var deployment = new ModelDeployment
            {
                Endpoint = endpoint,
                ProviderModelId = providerModelId,
                Priority = priority,
                IsEnabled = true,
                MaxConcurrentRequests = maxConcurrentRequests
            };

            deployment.RateLimitRules = rateLimitRules?.ToList() ?? new List<ModelRateLimitRule>();
            return deployment;
        }

        private static ChatRequest CreateChatRequest()
        {
            return new ChatRequest
            {
                Messages = new List<ChatMessageRequest>
                {
                    new ChatMessageRequest
                    {
                        Role = "user",
                        Content = "hello"
                    }
                }
            };
        }

        private sealed class FakeChatClientFactory : ChatClientFactory
        {
            private readonly Dictionary<string, IChatClient> _clients = new();

            public override IChatClient CreateClient(ModelDeployment deployment)
            {
                return _clients[deployment.ProviderModelId];
            }

            public void Register(string providerModelId, IChatClient client)
            {
                _clients.Add(providerModelId, client);
            }
        }

        private sealed class FakeHttpClientFactory : IHttpClientFactory
        {
            private readonly HttpClient _client;

            public FakeHttpClientFactory(HttpMessageHandler handler)
            {
                _client = new HttpClient(handler);
            }

            public HttpClient CreateClient(string name)
            {
                return _client;
            }
        }

        private sealed class FakeHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

            public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage>? handler)
            {
                _handler = handler ?? (_ => CreateModelsResponse("primary", "secondary"));
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(_handler(request));
            }
        }

        private sealed class FakeChatClient : IChatClient
        {
            private readonly Func<IEnumerable<ChatMessage>, Task<ChatResponse>> _handler;

            public FakeChatClient(Func<IEnumerable<ChatMessage>, Task<ChatResponse>> handler)
            {
                _handler = handler;
            }

            public bool IsDisposed { get; private set; }
            public int RequestCount { get; private set; }

            public Task<ChatResponse> GetResponseAsync(
                IEnumerable<ChatMessage> messages,
                ChatOptions? options = null,
                CancellationToken cancellationToken = default)
            {
                RequestCount++;
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
                IsDisposed = true;
            }
        }
    }
}

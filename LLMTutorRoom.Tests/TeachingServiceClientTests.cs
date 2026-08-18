using System.Net;
using System.Text;
using LLMTutorRoom.Services.Teaching;

namespace LLMTutorRoom.Tests;

public sealed class TeachingServiceClientTests
{
    [Fact]
    public async Task GetTestAsync_WithVersionNumber_RequestsExactVersion()
    {
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://teaching-service")
        };
        var client = new TeachingServiceClient(httpClient);

        var test = await client.GetTestAsync(
            "logical test",
            includeHidden: false,
            versionNumber: 12,
            CancellationToken.None);

        Assert.NotNull(test);
        Assert.Equal(
            "/internal/teaching/tests/logical%20test?includeHidden=false&versionNumber=12",
            handler.RequestUri?.PathAndQuery);
    }

    [Fact]
    public async Task GetTestAsync_WithoutVersionNumber_RequestsCurrentPublishedVersion()
    {
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://teaching-service")
        };
        var client = new TeachingServiceClient(httpClient);

        var test = await client.GetTestAsync(
            "test-1",
            includeHidden: true,
            versionNumber: null,
            CancellationToken.None);

        Assert.NotNull(test);
        Assert.Equal(
            "/internal/teaching/tests/test-1?includeHidden=true",
            handler.RequestUri?.PathAndQuery);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });
        }
    }
}

using System.Net;
using System.Text;
using LLMTutorRoom.Services.Reviews;

namespace LLMTutorRoom.Tests;

public sealed class ReviewServiceClientTests
{
    private const string EmptyPageJson =
        """
        {
          "nonTerminalReviews": [],
          "terminalReviews": [],
          "nextCursor": null,
          "aggregate": {
            "pendingReviews": 0,
            "averageScorePercentage": 0
          }
        }
        """;

    [Fact]
    public async Task GetTeacherReviewsAsync_DeserializesPagedContract()
    {
        using var handler = new RecordingHandler(EmptyPageJson);
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://review-service")
        };
        var client = new ReviewServiceClient(httpClient);

        var page = await client.GetTeacherReviewsAsync(
            "teacher id",
            CancellationToken.None);

        Assert.Empty(page.NonTerminalReviews);
        Assert.Empty(page.TerminalReviews);
        Assert.Null(page.NextCursor);
        Assert.Equal(
            "/internal/reviews/teachers/teacher%20id",
            handler.RequestUri?.PathAndQuery);
    }

    [Fact]
    public async Task GetTeacherReviewHistoryAsync_ForwardsOpaqueCursorAndVersionScope()
    {
        using var handler = new RecordingHandler(EmptyPageJson);
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://review-service")
        };
        var client = new ReviewServiceClient(httpClient);
        const string cursor = "AAECAwQFBgcICQoL";

        var result = await client.GetTeacherReviewHistoryAsync(
            "teacher",
            includeHistoricalVersions: false,
            pageSize: 100,
            cursor,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(
            "/internal/reviews/teachers/teacher/history"
            + "?includeHistoricalVersions=false&pageSize=100&cursor=AAECAwQFBgcICQoL",
            handler.RequestUri?.PathAndQuery);
    }

    private sealed class RecordingHandler(string responseBody) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            });
        }
    }
}

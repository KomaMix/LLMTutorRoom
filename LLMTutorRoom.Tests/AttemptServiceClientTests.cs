using System.Net;
using System.Text;
using AttemptService.Contracts.Enums;
using LLMTutorRoom.Services.Attempts;

namespace LLMTutorRoom.Tests;

public sealed class AttemptServiceClientTests
{
    private static readonly Guid AttemptId = Guid.Parse("17c0d763-729b-4b7c-aa68-c795d7c16d4c");

    private const string AttemptListJson =
        """
        [
          {
            "id": "17c0d763-729b-4b7c-aa68-c795d7c16d4c",
            "testId": "test-1",
            "testRevision": 3,
            "status": "in-progress",
            "startedAt": "2026-08-23T12:00:00+00:00",
            "endsAt": "2026-08-23T12:45:00+00:00",
            "submittedAt": null,
            "answers": {}
          }
        ]
        """;

    [Fact]
    public async Task GetStudentAttemptsAsync_RequestsStudentAndDeserializesAttempts()
    {
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://attempt-service")
        };
        var client = new AttemptServiceClient(httpClient);

        var attempts = await client.GetStudentAttemptsAsync(
            "student id",
            CancellationToken.None);

        var attempt = Assert.Single(attempts);
        Assert.Equal(AttemptId, attempt.Id);
        Assert.Equal(TestAttemptStatus.InProgress, attempt.Status);
        Assert.Equal(
            "/internal/attempts/students/student%20id",
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
                Content = new StringContent(
                    AttemptListJson,
                    Encoding.UTF8,
                    "application/json")
            });
        }
    }
}

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

    [Fact]
    public async Task GetTeacherAssignmentsAsync_RequestsTeachingAssignments()
    {
        using var handler = new RecordingHandler
        {
            ResponseJson = """
                [{"id":"10000000-0000-0000-0000-000000000001","groupId":"20000000-0000-0000-0000-000000000001","groupName":"Group","teacherUserId":"teacher","test":{"id":"test-1","versionNumber":3}}]
                """
        };
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://teaching-service")
        };
        var client = new TeachingServiceClient(httpClient);

        var assignments = await client.GetTeacherAssignmentsAsync("teacher 1", CancellationToken.None);

        Assert.Equal("/internal/teaching/teachers/teacher%201/assignments", handler.RequestUri?.PathAndQuery);
        var assignment = Assert.Single(assignments);
        Assert.Equal(Guid.Parse("20000000-0000-0000-0000-000000000001"), assignment.GroupId);
        Assert.Equal("test-1", assignment.Test.Id);
        Assert.Equal(3, assignment.Test.VersionNumber);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string ResponseJson { get; init; } = "{}";
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ResponseJson, Encoding.UTF8, "application/json")
            });
        }
    }
}

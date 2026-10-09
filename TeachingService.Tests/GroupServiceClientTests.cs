using System.Net;
using System.Text;
using TeachingService.Services;

namespace TeachingService.Tests;

public class GroupServiceClientTests
{
    [Fact]
    public async Task GetGroupAsync_RequestsCurrentGroupWithoutAuthentication()
    {
        var groupId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        using var handler = new RecordingHandler
        {
            ResponseJson = """
                {"id":"10000000-0000-0000-0000-000000000001","name":"Current group","teacherUserId":"teacher-1","studentUserIds":["student-1"],"revision":3}
                """
        };
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://group-service") };
        var client = new GroupServiceClient(httpClient);

        var group = await client.GetGroupAsync(groupId, CancellationToken.None);

        Assert.Equal($"/internal/groups/{groupId:D}", handler.RequestUri?.PathAndQuery);
        Assert.Null(handler.Authorization);
        Assert.NotNull(group);
        Assert.Equal(groupId, group.Id);
        Assert.Equal("teacher-1", group.TeacherUserId);
        Assert.Equal(["student-1"], group.StudentUserIds);
        Assert.Equal(3, group.Revision);
    }

    [Fact]
    public async Task GetStudentGroupsAsync_EscapesStudentIdAndPreservesCurrentMembership()
    {
        using var handler = new RecordingHandler
        {
            ResponseJson = """
                [{"id":"10000000-0000-0000-0000-000000000001","name":"Current group","studentUserIds":["student 1"]}]
                """
        };
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://group-service") };
        var client = new GroupServiceClient(httpClient);

        var groups = await client.GetStudentGroupsAsync("student 1", CancellationToken.None);

        Assert.Equal("/internal/students/student%201/groups", handler.RequestUri?.PathAndQuery);
        Assert.Null(handler.Authorization);
        Assert.Equal(["student 1"], Assert.Single(groups).StudentUserIds);
    }

    [Fact]
    public async Task GetGroupAsync_WhenMissingReturnsNull()
    {
        using var handler = new RecordingHandler { StatusCode = HttpStatusCode.NotFound };
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://group-service") };
        var client = new GroupServiceClient(httpClient);

        var group = await client.GetGroupAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(group);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WhenServiceUnavailablePropagatesFailure(bool singleGroup)
    {
        using var handler = new RecordingHandler { StatusCode = HttpStatusCode.ServiceUnavailable };
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://group-service") };
        var client = new GroupServiceClient(httpClient);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            if (singleGroup)
            {
                await client.GetGroupAsync(Guid.NewGuid(), CancellationToken.None);
            }
            else
            {
                await client.GetStudentGroupsAsync("student-1", CancellationToken.None);
            }
        });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
    }

    private class RecordingHandler : HttpMessageHandler
    {
        public HttpStatusCode StatusCode { get; init; } = HttpStatusCode.OK;
        public string ResponseJson { get; init; } = "{}";
        public Uri? RequestUri { get; private set; }
        public string? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Authorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(new HttpResponseMessage(StatusCode)
            {
                Content = new StringContent(ResponseJson, Encoding.UTF8, "application/json")
            });
        }
    }
}

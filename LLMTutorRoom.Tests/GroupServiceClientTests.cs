using System.Net;
using System.Text;
using LLMTutorRoom.Services.Groups;

namespace LLMTutorRoom.Tests;

public class GroupServiceClientTests
{
    [Theory]
    [InlineData(true, "/internal/teachers/user%201/groups")]
    [InlineData(false, "/internal/students/user%201/groups")]
    public async Task GetGroupsAsync_RequestsCatalogAndPreservesMembership(bool teacher, string path)
    {
        using var handler = new RecordingHandler
        {
            ResponseJson = """
                [{"id":"10000000-0000-0000-0000-000000000001","name":"Current group","teacherUserId":"teacher-1","studentUserIds":["student-1","student-2"],"revision":7}]
                """
        };
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://group-service") };
        var client = new GroupServiceClient(httpClient);

        var groups = teacher
            ? await client.GetTeacherGroupsAsync("user 1", CancellationToken.None)
            : await client.GetStudentGroupsAsync("user 1", CancellationToken.None);

        Assert.Equal(path, handler.RequestUri?.PathAndQuery);
        Assert.Null(handler.Authorization);
        var group = Assert.Single(groups);
        Assert.Equal("Current group", group.Name);
        Assert.Equal("teacher-1", group.TeacherUserId);
        Assert.Equal(["student-1", "student-2"], group.StudentUserIds);
        Assert.Equal(7, group.Revision);
    }

    [Fact]
    public async Task GetGroupAsync_WhenMissing_ReturnsNull()
    {
        var groupId = Guid.NewGuid();
        using var handler = new RecordingHandler { StatusCode = HttpStatusCode.NotFound };
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://group-service") };
        var client = new GroupServiceClient(httpClient);

        var group = await client.GetGroupAsync(groupId, CancellationToken.None);

        Assert.Null(group);
        Assert.Equal($"/internal/groups/{groupId:D}", handler.RequestUri?.PathAndQuery);
    }

    [Fact]
    public async Task GetTeacherGroupsAsync_WhenUnavailable_DoesNotReturnEmptyCatalog()
    {
        using var handler = new RecordingHandler { StatusCode = HttpStatusCode.ServiceUnavailable };
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://group-service") };
        var client = new GroupServiceClient(httpClient);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GetTeacherGroupsAsync("teacher", CancellationToken.None));

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

using System.Net;
using System.Net.Http.Json;
using GroupService.Contracts.Responses;
using LLMTutorRoom.Interfaces;

namespace LLMTutorRoom.Services.Groups;

public class GroupServiceClient : IGroupServiceClient
{
    private readonly HttpClient _httpClient;

    public GroupServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<StudyGroupResponse?> GetGroupAsync(
        Guid groupId,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(
            $"/internal/groups/{groupId:D}",
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StudyGroupResponse>(cancellationToken);
    }

    public Task<List<StudyGroupResponse>> GetTeacherGroupsAsync(
        string teacherUserId,
        CancellationToken cancellationToken)
    {
        return GetGroupsAsync(
            $"/internal/teachers/{Uri.EscapeDataString(teacherUserId)}/groups",
            cancellationToken);
    }

    public Task<List<StudyGroupResponse>> GetStudentGroupsAsync(
        string studentUserId,
        CancellationToken cancellationToken)
    {
        return GetGroupsAsync(
            $"/internal/students/{Uri.EscapeDataString(studentUserId)}/groups",
            cancellationToken);
    }

    private async Task<List<StudyGroupResponse>> GetGroupsAsync(
        string requestUri,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<StudyGroupResponse>>(cancellationToken)
            ?? new List<StudyGroupResponse>();
    }
}

using System.Net;
using System.Net.Http.Json;
using GroupService.Contracts.Responses;
using TeachingService.Interfaces;

namespace TeachingService.Services;

public class GroupServiceClient : IGroupServiceClient
{
    private readonly HttpClient _httpClient;

    public GroupServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<StudyGroupResponse?> GetGroupAsync(Guid groupId, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync($"/internal/groups/{groupId:D}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StudyGroupResponse>(cancellationToken);
    }

    public async Task<List<StudyGroupResponse>> GetStudentGroupsAsync(string studentUserId, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(
            $"/internal/students/{Uri.EscapeDataString(studentUserId)}/groups", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<StudyGroupResponse>>(cancellationToken) ?? [];
    }
}

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AttemptService.Interfaces;
using TeachingService.Contracts.Responses;

namespace AttemptService.Services;

public class TeachingServiceClient : ITeachingServiceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower)
        }
    };

    private readonly HttpClient _httpClient;

    public TeachingServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<TestAssignmentResponse>> GetStudentAssignmentsAsync(
        string studentUserId,
        CancellationToken cancellationToken)
    {
        var requestUri = $"/internal/teaching/students/{Uri.EscapeDataString(studentUserId)}/assignments";
        return await _httpClient.GetFromJsonAsync<List<TestAssignmentResponse>>(
            requestUri,
            JsonOptions,
            cancellationToken)
            ?? throw new HttpRequestException("TeachingService returned an empty assignment response.");
    }

    public async Task<TestAssignmentResponse?> GetStudentAssignmentAsync(
        Guid assignmentId,
        string studentUserId,
        CancellationToken cancellationToken)
    {
        var requestUri = $"/internal/teaching/students/{Uri.EscapeDataString(studentUserId)}" +
            $"/assignments/{assignmentId:D}";
        using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TestAssignmentResponse>(
            JsonOptions,
            cancellationToken);
    }
}

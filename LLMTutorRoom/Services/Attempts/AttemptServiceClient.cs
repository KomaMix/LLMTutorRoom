using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AttemptService.Contracts.Responses;
using LLMTutorRoom.Interfaces;

namespace LLMTutorRoom.Services.Attempts;

public class AttemptServiceClient : IAttemptServiceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower)
        }
    };

    private readonly HttpClient _httpClient;

    public AttemptServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<TestAttemptResponse>> GetStudentAttemptsAsync(
        string studentUserId,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(
            $"/internal/attempts/students/{Uri.EscapeDataString(studentUserId)}",
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<TestAttemptResponse>>(
                JsonOptions,
                cancellationToken)
            ?? [];
    }

    public async Task<List<TestAttemptResponse>> GetTeacherAttemptsAsync(
        string teacherUserId,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(
            $"/internal/attempts/teachers/{Uri.EscapeDataString(teacherUserId)}",
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<TestAttemptResponse>>(
                JsonOptions,
                cancellationToken)
            ?? [];
    }
}

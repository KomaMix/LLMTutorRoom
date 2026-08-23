using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AttemptService.Contracts.Responses;
using LLMTutorRoom.Interfaces;

namespace LLMTutorRoom.Services.Attempts;

public sealed class AttemptServiceClient(HttpClient httpClient) : IAttemptServiceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower)
        }
    };

    public async Task<List<TestAttemptResponse>> GetStudentAttemptsAsync(
        string studentUserId,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            $"/internal/attempts/students/{Uri.EscapeDataString(studentUserId)}",
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<TestAttemptResponse>>(
                JsonOptions,
                cancellationToken)
            ?? [];
    }
}

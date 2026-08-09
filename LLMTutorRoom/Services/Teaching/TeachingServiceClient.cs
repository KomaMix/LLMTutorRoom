using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TeachingService.Contracts.Models;

namespace LLMTutorRoom.Services.Teaching
{
    public sealed class TeachingServiceClient : ITeachingServiceClient
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

        public async Task<List<CourseTestDto>> GetTestsAsync(
            bool publishedOnly,
            bool includeHidden,
            CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"/internal/teaching/tests?publishedOnly={ToQueryValue(publishedOnly)}&includeHidden={ToQueryValue(includeHidden)}");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<List<CourseTestDto>>(
                    JsonOptions,
                    cancellationToken)
                ?? new List<CourseTestDto>();
        }

        public async Task<CourseTestDto?> GetTestAsync(
            string testId,
            bool includeHidden,
            CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"/internal/teaching/tests/{Uri.EscapeDataString(testId)}?includeHidden={ToQueryValue(includeHidden)}");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<CourseTestDto>(
                JsonOptions,
                cancellationToken);
        }

        private static string ToQueryValue(bool value)
        {
            return value
                ? "true"
                : "false";
        }
    }
}

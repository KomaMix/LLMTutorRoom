using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AttemptService.Interfaces;
using TeachingService.Contracts.Models;

namespace AttemptService.Services;

public sealed class TeachingServiceClient(HttpClient httpClient) : ITeachingServiceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower)
        }
    };

    public async Task<CourseTestDto?> GetTestAsync(
        Guid testId,
        bool includeHidden,
        int? versionNumber,
        CancellationToken cancellationToken)
    {
        var requestUri =
            $"/internal/teaching/tests/{testId:D}?includeHidden={ToQueryValue(includeHidden)}";
        if (versionNumber.HasValue)
        {
            requestUri +=
                $"&versionNumber={versionNumber.Value.ToString(CultureInfo.InvariantCulture)}";
        }

        using var response = await httpClient.GetAsync(requestUri, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CourseTestDto>(
            JsonOptions,
            cancellationToken);
    }

    private static string ToQueryValue(bool value)
    {
        return value ? "true" : "false";
    }
}

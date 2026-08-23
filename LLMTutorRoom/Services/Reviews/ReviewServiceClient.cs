using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LLMTutorRoom.Interfaces;
using ReviewService.Contracts.Responses;

namespace LLMTutorRoom.Services.Reviews;

public sealed class ReviewServiceClient : IReviewServiceClient
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly HttpClient _httpClient;

    public ReviewServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<ReviewPageResponse> GetTeacherReviewsAsync(
        string teacherUserId,
        CancellationToken cancellationToken)
    {
        return GetAsync<ReviewPageResponse>(
            $"/internal/reviews/teachers/{Uri.EscapeDataString(teacherUserId)}",
            cancellationToken);
    }

    public Task<ReviewPageResponse> GetStudentReviewsAsync(
        string studentUserId,
        CancellationToken cancellationToken)
    {
        return GetAsync<ReviewPageResponse>(
            $"/internal/reviews/students/{Uri.EscapeDataString(studentUserId)}",
            cancellationToken);
    }

    public Task<ReviewServiceResult<ReviewPageResponse>> GetTeacherReviewHistoryAsync(
        string teacherUserId,
        bool includeHistoricalVersions,
        int pageSize,
        string? cursor,
        CancellationToken cancellationToken)
    {
        var includeHistoricalValue = includeHistoricalVersions ? "true" : "false";
        var path = $"/internal/reviews/teachers/{Uri.EscapeDataString(teacherUserId)}/history"
            + $"?includeHistoricalVersions={includeHistoricalValue}&pageSize={pageSize}";
        return GetResultAsync<ReviewPageResponse>(
            AppendCursor(path, cursor),
            cancellationToken);
    }

    public Task<ReviewServiceResult<ReviewPageResponse>> GetStudentReviewHistoryAsync(
        string studentUserId,
        int pageSize,
        string? cursor,
        CancellationToken cancellationToken)
    {
        var path = $"/internal/reviews/students/{Uri.EscapeDataString(studentUserId)}/history"
            + $"?pageSize={pageSize}";
        return GetResultAsync<ReviewPageResponse>(
            AppendCursor(path, cursor),
            cancellationToken);
    }

    public Task<List<TeacherModelAccessResponse>> GetTeacherModelAccessAsync(
        string teacherUserId,
        bool includeDisabled,
        CancellationToken cancellationToken)
    {
        var includeDisabledValue = includeDisabled ? "true" : "false";
        return GetListAsync<TeacherModelAccessResponse>(
            $"/internal/model-access/teachers/{Uri.EscapeDataString(teacherUserId)}"
            + $"?includeDisabled={includeDisabledValue}",
            cancellationToken);
    }

    private async Task<List<T>> GetListAsync<T>(
        string path,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<T>>(
                JsonOptions,
                cancellationToken)
            ?? new List<T>();
    }

    private async Task<T> GetAsync<T>(
        string path,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>(
                JsonOptions,
                cancellationToken)
            ?? throw new HttpRequestException("Review service returned an empty response.");
    }

    private async Task<ReviewServiceResult<T>> GetResultAsync<T>(
        string path,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(path, cancellationToken);
        return await ReadResultAsync<T>(response, cancellationToken);
    }

    private static string AppendCursor(string path, string? cursor)
    {
        return string.IsNullOrWhiteSpace(cursor)
            ? path
            : $"{path}&cursor={Uri.EscapeDataString(cursor)}";
    }

    private static async Task<ReviewServiceResult<T>> ReadResultAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            return new ReviewServiceResult<T>(
                response.StatusCode,
                default,
                await response.Content.ReadAsStringAsync(cancellationToken));
        }

        var value = await response.Content.ReadFromJsonAsync<T>(
            JsonOptions,
            cancellationToken);
        return new ReviewServiceResult<T>(response.StatusCode, value, string.Empty);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower));
        return options;
    }
}

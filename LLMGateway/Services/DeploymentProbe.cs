using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using LLMGateway.Data.Models;
using LLMGateway.DTOs.Models;

namespace LLMGateway.Services;

public sealed class DeploymentProbe(IHttpClientFactory clientFactory) : IDeploymentProbe
{
    public async Task<DeploymentCheckResponse> CheckAsync(ModelDeployment deployment, CancellationToken ct)
    {
        var timer = Stopwatch.StartNew();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, deployment.Endpoint.TrimEnd('/') + "/models");
            if (!string.IsNullOrWhiteSpace(deployment.ApiKey))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", deployment.ApiKey);
            using var client = clientFactory.CreateClient();
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode)
                return Result(false, $"Провайдер вернул HTTP {(int)response.StatusCode}.");
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var catalog = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token);
            if (catalog.RootElement.ValueKind != JsonValueKind.Object
                || !catalog.RootElement.TryGetProperty("data", out var models) || models.ValueKind != JsonValueKind.Array)
                return Result(false, "Провайдер вернул некорректный каталог моделей.");
            var found = models.EnumerateArray().Any(model => model.ValueKind == JsonValueKind.Object
                && model.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String
                && string.Equals(id.GetString(), deployment.ProviderModelId, StringComparison.Ordinal));
            return Result(found, found ? "Подключение установлено, модель доступна." : "Указанная модель отсутствует в каталоге провайдера.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return Result(false, "Истекло время ожидания провайдера."); }
        catch (HttpRequestException) { return Result(false, "Не удалось подключиться к провайдеру."); }
        catch (JsonException) { return Result(false, "Провайдер вернул некорректный каталог моделей."); }
        catch (UriFormatException) { return Result(false, "Указан некорректный адрес провайдера."); }

        DeploymentCheckResponse Result(bool available, string message) => new(available, message, DateTimeOffset.UtcNow, timer.ElapsedMilliseconds);
    }
}

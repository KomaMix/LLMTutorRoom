using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ReviewService.Helpers;
using ReviewService.Interfaces;
using ReviewService.Models.Reviews;
using ReviewService.Options;

namespace ReviewService.Services;

public sealed class LlmGatewayReviewClient(
    HttpClient httpClient,
    IOptions<ReviewProcessingOptions> options) : ILlmGatewayReviewClient
{
    private readonly ReviewProcessingOptions _options = options.Value;

    public async Task<LlmTaskReviewResult> ReviewFreeTextAnswerAsync(
        string modelKey,
        ReviewTask task,
        CancellationToken cancellationToken)
    {
        if (!_options.LlmGatewayEnabled)
            throw new InvalidOperationException("LLMGateway review processing is disabled.");

        if (string.IsNullOrWhiteSpace(modelKey))
            throw new InvalidOperationException("Model key is required for LLM task checks.");

        var request = new LlmGatewayChatRequest(
            [
                new LlmGatewayChatMessage(
                    "system",
                    """
                    Ты проверяешь ответ ученика. Верни только JSON без Markdown.
                    Схема: {"score": number, "feedback": string, "findings": string[]}
                    score должен быть от 0 до maxScore. findings: 1-5 коротких пунктов.
                    """),
                new LlmGatewayChatMessage(
                    "user",
                    JsonSerializer.Serialize(new
                    {
                        taskTitle = task.TaskTitle,
                        taskPrompt = task.TaskPrompt,
                        maxScore = task.MaxScore,
                        studentAnswer = task.StudentAnswer
                    }, JsonHelper.Options))
            ],
            0.1f);

        using var response = await httpClient.PostAsJsonAsync(
            $"/api/chat/{Uri.EscapeDataString(modelKey)}",
            request,
            JsonHelper.Options,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var gatewayResponse = await response.Content.ReadFromJsonAsync<LlmGatewayChatResponse>(
            JsonHelper.Options,
            cancellationToken);
        if (gatewayResponse is null || string.IsNullOrWhiteSpace(gatewayResponse.Text))
            throw new InvalidOperationException("LLMGateway returned an empty review response.");

        return ParseReviewJson(gatewayResponse.Text);
    }

    private static LlmTaskReviewResult ParseReviewJson(string text)
    {
        var trimmed = text.Trim();
        var jsonStart = trimmed.IndexOf('{');
        var jsonEnd = trimmed.LastIndexOf('}');
        if (jsonStart < 0 || jsonEnd < jsonStart)
            throw new JsonException("LLM review response does not contain a JSON object.");

        return JsonSerializer.Deserialize<LlmTaskReviewResult>(
                trimmed[jsonStart..(jsonEnd + 1)],
                JsonHelper.Options)
            ?? throw new JsonException("LLM review JSON is empty.");
    }

    private sealed record LlmGatewayChatRequest(
        List<LlmGatewayChatMessage> Messages,
        float? Temperature);

    private sealed record LlmGatewayChatMessage(string Role, string Content);
    private sealed record LlmGatewayChatResponse(string Model, string Text);
}

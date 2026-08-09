using System.Net.Http.Json;
using System.Text.Json;
using LLMTutorRoom.Models;
using Microsoft.Extensions.Options;

namespace LLMTutorRoom.Services.ReviewProcessing
{
    public sealed class LlmGatewayReviewClient
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly HttpClient _httpClient;
        private readonly ReviewProcessingOptions _options;

        public LlmGatewayReviewClient(
            HttpClient httpClient,
            IOptions<ReviewProcessingOptions> options)
        {
            _httpClient = httpClient;
            _options = options.Value;
        }

        public async Task<LlmTaskReviewResult> ReviewFreeTextAnswerAsync(
            TaskReviewResult task,
            string answer,
            CancellationToken cancellationToken)
        {
            if (!_options.LlmGatewayEnabled)
            {
                throw new LlmGatewayReviewDisabledException(
                    "LLMGateway review processing is disabled.");
            }

            if (string.IsNullOrWhiteSpace(_options.LlmModelKey))
            {
                throw new InvalidOperationException(
                    "ReviewProcessing:LlmModelKey is required for LLM task checks.");
            }

            var request = new LlmGatewayChatRequest
            {
                Temperature = 0.1f,
                Messages = new[]
                {
                    new LlmGatewayChatMessage
                    {
                        Role = "system",
                        Content = """
                            Ты проверяешь ответ ученика. Верни только JSON без Markdown.
                            Схема: {"score": number, "feedback": string, "findings": string[]}
                            score должен быть от 0 до maxScore. findings: 1-5 коротких пунктов.
                            """
                    },
                    new LlmGatewayChatMessage
                    {
                        Role = "user",
                        Content = JsonSerializer.Serialize(new
                        {
                            taskTitle = task.TaskTitle,
                            taskPrompt = task.TaskPrompt,
                            maxScore = task.MaxScore,
                            studentAnswer = answer
                        }, JsonOptions)
                    }
                }
            };

            using var response = await _httpClient.PostAsJsonAsync(
                $"/api/chat/{Uri.EscapeDataString(_options.LlmModelKey)}",
                request,
                JsonOptions,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var gatewayResponse = await response.Content.ReadFromJsonAsync<LlmGatewayChatResponse>(
                JsonOptions,
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

            var json = trimmed[jsonStart..(jsonEnd + 1)];
            return JsonSerializer.Deserialize<LlmTaskReviewResult>(json, JsonOptions)
                ?? throw new JsonException("LLM review JSON is empty.");
        }

        private sealed class LlmGatewayChatRequest
        {
            public IReadOnlyCollection<LlmGatewayChatMessage> Messages { get; set; }
                = Array.Empty<LlmGatewayChatMessage>();

            public float? Temperature { get; set; }
        }

        private sealed class LlmGatewayChatMessage
        {
            public string Role { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
        }

        private sealed class LlmGatewayChatResponse
        {
            public string Model { get; set; } = string.Empty;
            public string Text { get; set; } = string.Empty;
        }
    }

    public sealed class LlmGatewayReviewDisabledException : Exception
    {
        public LlmGatewayReviewDisabledException(string message) : base(message)
        {
        }
    }
}

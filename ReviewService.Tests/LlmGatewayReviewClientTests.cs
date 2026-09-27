using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OptionsFactory = Microsoft.Extensions.Options.Options;
using ReviewService.Contracts.Events;
using ReviewService.Helpers;
using ReviewService.Models.Reviews;
using ReviewService.Options;
using ReviewService.Services;

namespace ReviewService.Tests;

public sealed class LlmGatewayReviewClientTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Review_SendsExampleContextBeforeCurrentAnswer(
        bool hasTestExamples,
        bool hasTaskExamples)
    {
        var generalExample = new GradingExampleSnapshot
        {
            TaskPrompt = "Explain linear search.",
            StudentAnswer = "Check each element.",
            Score = 2,
            MaxScore = 5,
            Feedback = "The idea is correct, but the complexity is missing."
        };
        var taskExample = new GradingExampleSnapshot
        {
            StudentAnswer = "Discard half the range each time.",
            Score = 6,
            MaxScore = 10,
            Feedback = "Missing the sorted-input requirement."
        };
        List<GradingExampleSnapshot> generalExamples = hasTestExamples ? [generalExample] : [];
        List<GradingExampleSnapshot> localExamples = hasTaskExamples ? [taskExample] : [];
        var task = new ReviewTask
        {
            TaskTitle = "Binary search",
            TaskPrompt = "Explain binary search and its requirements.",
            MaxScore = 20,
            StudentAnswer = "My own answer, different from the examples.",
            GradingExamplesJson = JsonSerializer.Serialize(localExamples, JsonHelper.Options)
        };
        var originalExamplesJson = task.GradingExamplesJson;
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://gateway.test") };
        var client = new LlmGatewayReviewClient(httpClient, OptionsFactory.Create(new ReviewProcessingOptions
        {
            LlmGatewayEnabled = true
        }));

        var result = await client.ReviewFreeTextAnswerAsync(
            "grading-model", task, generalExamples, CancellationToken.None);

        Assert.Equal("/api/chat/grading-model", handler.RequestUri?.AbsolutePath);
        var messages = handler.Request.GetProperty("messages");
        Assert.Equal(3, messages.GetArrayLength());
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        Assert.Equal("user", messages[2].GetProperty("role").GetString());

        using var exampleContext = JsonDocument.Parse(messages[1].GetProperty("content").GetString()!);
        var examplesPayload = exampleContext.RootElement;
        Assert.False(examplesPayload.TryGetProperty("studentAnswer", out _));
        Assert.False(examplesPayload.TryGetProperty("taskPrompt", out _));
        var testExamples = examplesPayload.GetProperty("testGradingExamples")
            .Deserialize<List<GradingExampleSnapshot>>(JsonHelper.Options)!;
        var taskExamples = examplesPayload.GetProperty("taskGradingExamples")
            .Deserialize<List<GradingExampleSnapshot>>(JsonHelper.Options)!;
        Assert.Equal(generalExamples, testExamples);
        Assert.Equal(localExamples.Select(example => example with { TaskPrompt = task.TaskPrompt }), taskExamples);

        using var currentAnswer = JsonDocument.Parse(messages[2].GetProperty("content").GetString()!);
        var payload = currentAnswer.RootElement;
        Assert.Equal(task.TaskTitle, payload.GetProperty("taskTitle").GetString());
        Assert.Equal(task.TaskPrompt, payload.GetProperty("taskPrompt").GetString());
        Assert.Equal(20m, payload.GetProperty("maxScore").GetDecimal());
        Assert.Equal(task.StudentAnswer, payload.GetProperty("studentAnswer").GetString());
        Assert.False(payload.TryGetProperty("testGradingExamples", out _));
        Assert.False(payload.TryGetProperty("taskGradingExamples", out _));
        Assert.Equal(originalExamplesJson, task.GradingExamplesJson);
        Assert.Equal(12m, result.Score);
        Assert.Equal("Checked current answer.", result.Feedback);
        Assert.Equal(new[] { "Missing a requirement." }, result.Findings);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public JsonElement Request { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            Request = document.RootElement.Clone();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    model = "grading-model",
                    text = """{"score":12,"feedback":"Checked current answer.","findings":["Missing a requirement."]}"""
                })
            };
        }
    }
}

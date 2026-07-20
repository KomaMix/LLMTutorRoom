using LLMGateway.DTOs.Chat;
using LLMGateway.Enums;
using LLMGateway.Services;
using Microsoft.AspNetCore.Mvc;

namespace LLMGateway.Controllers
{
    [ApiController]
    [Route("api/chat")]
    public class ChatController : ControllerBase
    {
        private readonly ChatExecutionService _chatExecutionService;

        public ChatController(ChatExecutionService chatExecutionService)
        {
            _chatExecutionService = chatExecutionService;
        }

        [HttpPost]
        public async Task<ActionResult<ChatCompletionResponse>> Chat(
            [FromBody] ChatRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _chatExecutionService.ExecuteAsync(request, cancellationToken);
            return result.Status switch
            {
                ChatExecutionStatus.Completed => Ok(result.Response),
                ChatExecutionStatus.ModelNotFound =>
                    Problem(
                        statusCode: StatusCodes.Status404NotFound,
                        title: "Model not found"),
                ChatExecutionStatus.NoAvailableDeployment =>
                    Problem(
                        statusCode: StatusCodes.Status503ServiceUnavailable,
                        title: "No deployment available"),
                ChatExecutionStatus.RateLimitExceeded =>
                    Problem(
                        statusCode: StatusCodes.Status429TooManyRequests,
                        title: "Rate limit exceeded"),
                ChatExecutionStatus.ConcurrencyLimitExceeded =>
                    Problem(
                        statusCode: StatusCodes.Status429TooManyRequests,
                        title: "Concurrency limit exceeded"),
                ChatExecutionStatus.ProviderFailed =>
                    Problem(
                        statusCode: StatusCodes.Status502BadGateway,
                        title: "Provider request failed"),
                ChatExecutionStatus.ProviderTimedOut =>
                    Problem(
                        statusCode: StatusCodes.Status504GatewayTimeout,
                        title: "Provider request timed out"),
                _ => throw new InvalidOperationException($"Unsupported chat execution status: {result.Status}")
            };
        }
    }
}

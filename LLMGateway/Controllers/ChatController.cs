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
            var (response, status) = await _chatExecutionService.ExecuteAsync(request, cancellationToken);
            return status switch
            {
                ChatExecutionStatus.Completed => Ok(response),
                ChatExecutionStatus.NoAvailableDeployment =>
                    Conflict($"No enabled deployment for model '{request.Model}' is available."),
                ChatExecutionStatus.RateLimitExceeded =>
                    StatusCode(StatusCodes.Status429TooManyRequests, "Rate limit exceeded."),
                _ => throw new InvalidOperationException($"Unsupported chat execution status: {status}")
            };
        }
    }
}

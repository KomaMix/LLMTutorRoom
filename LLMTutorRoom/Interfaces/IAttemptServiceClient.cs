using AttemptService.Contracts.Responses;

namespace LLMTutorRoom.Interfaces;

public interface IAttemptServiceClient
{
    Task<List<TestAttemptResponse>> GetStudentAttemptsAsync(
        string studentUserId,
        CancellationToken cancellationToken);
}

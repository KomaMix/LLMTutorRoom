using AttemptService.Contracts.Responses;
using AttemptService.Enums;
using AttemptService.Models;

namespace AttemptService.Interfaces;

public interface IAttemptLifecycleService
{
    Task<List<TestAttemptResponse>> GetStudentAttemptsAsync(
        string studentUserId,
        CancellationToken cancellationToken);

    Task<TestAttemptResponse?> GetAttemptAsync(
        Guid attemptId,
        string studentUserId,
        CancellationToken cancellationToken);

    Task<Result<AttemptOperationStatus, TestAttemptResponse>> StartAttemptAsync(
        Guid testId,
        string studentUserId,
        int? expectedVersionNumber,
        CancellationToken cancellationToken);

    Task<Result<AttemptOperationStatus, TestAttemptResponse>> SaveAnswersAsync(
        Guid attemptId,
        string studentUserId,
        Dictionary<string, string> answers,
        CancellationToken cancellationToken);

    Task<Result<AttemptOperationStatus, TestAttemptResponse>> SubmitAttemptAsync(
        Guid attemptId,
        string studentUserId,
        string studentUserName,
        CancellationToken cancellationToken);
}

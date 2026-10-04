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

    Task<List<TestAttemptResponse>> GetTeacherAttemptsAsync(
        string teacherUserId,
        CancellationToken cancellationToken);

    Task<Result<AttemptOperationStatus, TestAttemptResponse>> StartAttemptAsync(
        Guid assignmentId,
        string studentUserId,
        string studentUserName,
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

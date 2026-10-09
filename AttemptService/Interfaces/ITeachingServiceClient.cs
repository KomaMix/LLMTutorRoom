using TeachingService.Contracts.Responses;

namespace AttemptService.Interfaces;

public interface ITeachingServiceClient
{
    Task<List<TestAssignmentResponse>> GetStudentAssignmentsAsync(
        string studentUserId,
        CancellationToken cancellationToken);

    Task<TestAssignmentResponse?> GetStudentAssignmentAsync(
        Guid assignmentId,
        string studentUserId,
        CancellationToken cancellationToken);
}

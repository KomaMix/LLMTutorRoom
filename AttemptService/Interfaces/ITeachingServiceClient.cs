using TeachingService.Contracts.Responses;

namespace AttemptService.Interfaces;

public interface ITeachingServiceClient
{
    Task<TestAssignmentResponse?> GetStudentAssignmentAsync(
        Guid assignmentId,
        string studentUserId,
        CancellationToken cancellationToken);
}

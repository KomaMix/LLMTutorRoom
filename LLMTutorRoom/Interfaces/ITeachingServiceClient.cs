using TeachingService.Contracts.Models;
using TeachingService.Contracts.Responses;

namespace LLMTutorRoom.Interfaces
{
    public interface ITeachingServiceClient
    {
        Task<List<CourseTestDto>> GetTestsAsync(
            bool publishedOnly,
            bool includeHidden,
            CancellationToken cancellationToken);

        Task<CourseTestDto?> GetTestAsync(
            string testId,
            bool includeHidden,
            int? versionNumber,
            CancellationToken cancellationToken);

        Task<List<TestAssignmentResponse>> GetStudentAssignmentsAsync(
            string studentUserId,
            CancellationToken cancellationToken);

        Task<TestAssignmentResponse?> GetAssignmentAsync(
            Guid assignmentId,
            bool includeHidden,
            CancellationToken cancellationToken);

        Task<List<TestAssignmentResponse>> GetTeacherAssignmentsAsync(
            string teacherUserId,
            CancellationToken cancellationToken);
    }
}

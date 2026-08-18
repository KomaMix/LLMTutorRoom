using TeachingService.Contracts.Models;

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
    }
}

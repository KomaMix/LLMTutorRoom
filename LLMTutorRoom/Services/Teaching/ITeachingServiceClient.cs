using TeachingService.Contracts.Models;

namespace LLMTutorRoom.Services.Teaching
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
            CancellationToken cancellationToken);
    }
}

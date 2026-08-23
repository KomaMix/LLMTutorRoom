using TeachingService.Contracts.Models;

namespace AttemptService.Interfaces;

public interface ITeachingServiceClient
{
    Task<CourseTestDto?> GetTestAsync(
        Guid testId,
        bool includeHidden,
        int? versionNumber,
        CancellationToken cancellationToken);
}

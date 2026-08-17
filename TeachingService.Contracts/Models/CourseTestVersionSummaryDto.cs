using TeachingService.Contracts.Enums;

namespace TeachingService.Contracts.Models
{
    public sealed record CourseTestVersionSummaryDto(
        int VersionNumber,
        CourseTestStatus Status,
        string Title,
        DateTimeOffset CreatedAt,
        DateTimeOffset? PublishedAt);
}

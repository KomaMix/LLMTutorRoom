using ReviewService.Models.Reviews;

namespace ReviewService.Interfaces;

public interface IReviewCreationService
{
    Task<Review> CreateAsync(
        TestReviewPolicy policy,
        Guid attemptId,
        Guid assignmentId,
        Guid groupId,
        string studentUserId,
        string studentName,
        Dictionary<string, string> answers,
        DateTimeOffset submittedAt,
        CancellationToken cancellationToken);
}

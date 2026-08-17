using TeachingService.Models;

namespace TeachingService.Interfaces
{
    public interface IReviewPolicyOutboxWriter
    {
        void StagePublishedRevision(CourseTest test, CourseTestVersion version);
    }
}

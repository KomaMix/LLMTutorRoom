using AttemptService.Contracts.Responses;
using ReviewService.Contracts.Responses;
using TeachingService.Contracts.Models;

namespace LLMTutorRoom.Models
{
    public sealed class ClassroomOverview
    {
        public List<CourseTestDto> Tests { get; set; } = new();
        public List<LanguageModel> Models { get; set; } = new();
        public List<ReviewResponse> Reviews { get; set; } = new();
        public List<TestAttemptResponse> Attempts { get; set; } = new();
        public DashboardMetrics Metrics { get; set; } = new();
        public string? TerminalReviewsNextCursor { get; set; }
    }

    public sealed record ReviewHistoryPageResponse(
        List<ReviewResponse> Reviews,
        string? NextCursor);

    public sealed class DashboardMetrics
    {
        public int ActiveTests { get; set; }
        public int Tasks { get; set; }
        public int PendingReviews { get; set; }
        public decimal AverageScore { get; set; }
    }

    public sealed class LanguageModel
    {
        public string Key { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int Priority { get; set; }
        public int MaxConcurrentRequests { get; set; }
        public int PeriodSeconds { get; set; }
        public int MaxChecks { get; set; }
        public int UsedChecks { get; set; }
        public int RemainingChecks { get; set; }
        public DateTimeOffset? PeriodEndsAt { get; set; }
    }
}

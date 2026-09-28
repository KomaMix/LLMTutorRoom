using AttemptService.Contracts.Responses;
using ReviewService.Contracts.Responses;
using TeachingService.Contracts.Enums;
using TeachingService.Contracts.Models;

namespace LLMTutorRoom.Models
{
    public sealed record TeacherClassroomOverviewResponse(
        List<CourseTestDto> Tests,
        List<LanguageModel> Models,
        List<ReviewResponse> Reviews,
        List<TestAttemptResponse> Attempts,
        DashboardMetrics Metrics,
        string? TerminalReviewsNextCursor);

    public sealed record StudentClassroomOverviewResponse(
        List<StudentCourseTestResponse> Tests,
        List<LanguageModel> Models,
        List<ReviewResponse> Reviews,
        List<TestAttemptResponse> Attempts,
        DashboardMetrics Metrics,
        string? TerminalReviewsNextCursor);

    public sealed record StudentCourseTestResponse(
        string Id,
        string TeacherUserId,
        string Title,
        string Subject,
        CourseTestStatus Status,
        DateTimeOffset Deadline,
        int TimeLimitMinutes,
        string Summary,
        int? LlmModelId,
        int VersionNumber,
        decimal TotalPoints,
        List<StudentTestTaskResponse> Tasks);

    public sealed record StudentTestTaskResponse(
        string Id,
        TestTaskType Type,
        TestTaskCheckMode CheckMode,
        string Title,
        string Prompt,
        decimal MaxPoints,
        decimal WrongAnswerPenalty,
        bool IsHidden,
        DateTimeOffset CreatedAt,
        List<StudentAnswerOptionResponse> Options);

    public sealed record StudentAnswerOptionResponse(
        string Id,
        string Text);

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
        public int Id { get; set; }
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

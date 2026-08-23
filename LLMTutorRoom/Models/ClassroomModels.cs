using System.Text.Json.Serialization;
using LLMTutorRoom.Enums;
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

    public sealed class TestAttempt
    {
        public int Id { get; set; }
        public string TestId { get; set; } = string.Empty;

        public int TestRevision { get; set; }
        public string StudentUserId { get; set; } = string.Empty;
        public TestAttemptStatus Status { get; set; } = TestAttemptStatus.InProgress;
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset EndsAt { get; set; }
        public DateTimeOffset? SubmittedAt { get; set; }

        [JsonIgnore]
        public string AnswersJson { get; set; } = "{}";

        [JsonIgnore]
        public string AllowedTaskIdsJson { get; set; } = "[]";

        [JsonIgnore]
        public int StateRevision { get; set; }
    }

    public sealed class AttemptSubmissionOutboxMessage
    {
        public Guid Id { get; set; }
        public int AttemptId { get; set; }
        public DateTimeOffset OccurredAt { get; set; }
        public string PayloadJson { get; set; } = string.Empty;
        public int PublishAttempts { get; set; }
        public DateTimeOffset? NextPublishAt { get; set; }
        public DateTimeOffset? PublishedAt { get; set; }
        public string LastError { get; set; } = string.Empty;
    }

    public sealed class TestAttemptResponse
    {
        public int Id { get; set; }
        public string TestId { get; set; } = string.Empty;
        public int TestRevision { get; set; }
        public TestAttemptStatus Status { get; set; }
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset EndsAt { get; set; }
        public DateTimeOffset? SubmittedAt { get; set; }
        public Dictionary<string, string> Answers { get; set; } = new();
    }

    public sealed record StartAttemptResult(
        StartAttemptOutcome Outcome,
        TestAttemptResponse? Attempt = null);
}

using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;
using TeachingService.Contracts.Enums;
using TeachingService.Contracts.Models;

namespace LLMTutorRoom.Models
{
    public enum SubmissionReviewStatus
    {
        Checked,
        Queued,
        Processing,
        RetryScheduled,
        ManualReview,
        Failed
    }

    public enum TaskReviewResultStatus
    {
        Pending,
        Processing,
        Succeeded,
        RetryScheduled,
        ManualReview,
        Failed
    }

    public enum TestAttemptStatus
    {
        InProgress,
        Submitted,
        Expired
    }

    public sealed class ClassroomOverview
    {
        public List<CourseTestDto> Tests { get; set; } = new();
        public List<LanguageModel> Models { get; set; } = new();
        public List<SubmissionReview> Reviews { get; set; } = new();
        public List<TestAttemptResponse> Attempts { get; set; } = new();
        public DashboardMetrics Metrics { get; set; } = new();
    }

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
        public string Provider { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int Priority { get; set; }
        public int MaxConcurrentRequests { get; set; }
        public int PeriodSeconds { get; set; }
        public int MaxChecks { get; set; }
        public int UsedChecks { get; set; }
        public int RemainingChecks { get; set; }
        public DateTimeOffset? PeriodEndsAt { get; set; }
    }

    public sealed class TeacherModelAccess
    {
        public int Id { get; set; }
        public string TeacherUserId { get; set; } = string.Empty;
        public string ModelKey { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public int PeriodSeconds { get; set; } = 30 * 24 * 60 * 60;
        public int MaxChecks { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public sealed class TeacherModelUsage
    {
        public int Id { get; set; }
        public string TeacherUserId { get; set; } = string.Empty;
        public string ModelKey { get; set; } = string.Empty;
        public DateTimeOffset PeriodStart { get; set; }
        public int PeriodSeconds { get; set; }
        public int UsedChecks { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public sealed class SubmissionReview
    {
        public int Id { get; set; }
        public int? AttemptId { get; set; }
        public TestAttempt? Attempt { get; set; }
        public string TestId { get; set; } = string.Empty;
        public string TestTitle { get; set; } = string.Empty;
        public string TeacherUserId { get; set; } = string.Empty;
        public string StudentUserId { get; set; } = string.Empty;
        public string? StudentName { get; set; }
        public SubmissionReviewStatus Status { get; set; } = SubmissionReviewStatus.Checked;
        public string ModelKey { get; set; } = string.Empty;
        public DateTimeOffset SubmittedAt { get; set; }
        public DateTimeOffset? QueuedAt { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public DateTimeOffset? NextRetryAt { get; set; }
        public DateTimeOffset? ProcessingLeaseExpiresAt { get; set; }
        public DateTimeOffset? LastEnqueuedAt { get; set; }
        public DateTimeOffset? LlmQuotaReservedAt { get; set; }
        public string LlmQuotaReservationError { get; set; } = string.Empty;
        public int ProcessingAttempts { get; set; }
        public string LastError { get; set; } = string.Empty;
        public decimal Score { get; set; }
        public decimal MaxScore { get; set; }
        public string Summary { get; set; } = string.Empty;
        public List<TaskReviewResult> TaskResults { get; set; } = new();
    }

    public sealed class TestAttempt
    {
        public int Id { get; set; }
        public string TestId { get; set; } = string.Empty;
        public string StudentUserId { get; set; } = string.Empty;
        public TestAttemptStatus Status { get; set; } = TestAttemptStatus.InProgress;
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset EndsAt { get; set; }
        public DateTimeOffset? SubmittedAt { get; set; }

        [JsonIgnore]
        public string AnswersJson { get; set; } = "{}";
    }

    public sealed class TestAttemptResponse
    {
        public int Id { get; set; }
        public string TestId { get; set; } = string.Empty;
        public TestAttemptStatus Status { get; set; }
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset EndsAt { get; set; }
        public DateTimeOffset? SubmittedAt { get; set; }
        public Dictionary<string, string> Answers { get; set; } = new();
    }

    public sealed class TaskReviewResult
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public int Id { get; set; }
        public int SubmissionReviewId { get; set; }
        public string TaskId { get; set; } = string.Empty;
        public string TaskTitle { get; set; } = string.Empty;
        public string TaskPrompt { get; set; } = string.Empty;
        public TestTaskCheckMode CheckMode { get; set; } = TestTaskCheckMode.Auto;
        public TaskReviewResultStatus Status { get; set; } = TaskReviewResultStatus.Pending;
        public int Attempts { get; set; }
        public DateTimeOffset? NextRetryAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public string LastError { get; set; } = string.Empty;
        public decimal Score { get; set; }
        public decimal MaxScore { get; set; }
        public string Feedback { get; set; } = string.Empty;

        [JsonIgnore]
        public string FindingsJson { get; set; } = "[]";

        [NotMapped]
        public List<string> Findings
        {
            get
            {
                if (string.IsNullOrWhiteSpace(FindingsJson))
                    return new List<string>();

                return JsonSerializer.Deserialize<List<string>>(FindingsJson, JsonOptions)
                    ?? new List<string>();
            }
            set
            {
                FindingsJson = JsonSerializer.Serialize(value ?? new List<string>(), JsonOptions);
            }
        }
    }

}

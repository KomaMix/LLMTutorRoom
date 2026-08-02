using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LLMTutorRoom.Models
{
    public enum CourseTestStatus
    {
        Draft,
        Published
    }

    public enum TestTaskType
    {
        SingleChoice,
        MultipleChoice,
        FreeText
    }

    public enum SubmissionReviewStatus
    {
        Checked,
        Queued,
        ManualReview
    }

    public enum UserRole
    {
        Admin,
        Teacher,
        Student
    }

    public sealed class ClassroomOverview
    {
        public IReadOnlyCollection<CourseTest> Tests { get; set; } = Array.Empty<CourseTest>();
        public IReadOnlyCollection<LanguageModel> Models { get; set; } = Array.Empty<LanguageModel>();
        public IReadOnlyCollection<SubmissionReview> Reviews { get; set; } = Array.Empty<SubmissionReview>();
        public DashboardMetrics Metrics { get; set; } = new();
    }

    public sealed class DashboardMetrics
    {
        public int ActiveTests { get; set; }
        public int Tasks { get; set; }
        public int PendingReviews { get; set; }
        public decimal AverageScore { get; set; }
    }

    public sealed class CourseTest
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public CourseTestStatus Status { get; set; } = CourseTestStatus.Draft;
        public DateTimeOffset Deadline { get; set; }
        public int TimeLimitMinutes { get; set; }
        public string Summary { get; set; } = string.Empty;
        public List<TestTask> Tasks { get; set; } = new();

        [NotMapped]
        public decimal TotalPoints
        {
            get
            {
                return Tasks
                    .Where(t => !t.IsHidden)
                    .Sum(t => t.MaxPoints);
            }
        }
    }

    public sealed class TestTask
    {
        public string Id { get; set; } = string.Empty;
        public string CourseTestId { get; set; } = string.Empty;
        public TestTaskType Type { get; set; } = TestTaskType.FreeText;
        public string Title { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
        public decimal MaxPoints { get; set; }
        public decimal WrongAnswerPenalty { get; set; }
        public bool IsHidden { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public List<AnswerOption> Options { get; set; } = new();

        [NotMapped]
        public List<string> CorrectOptionIds
        {
            get
            {
                return Options
                    .Where(option => option.IsCorrect)
                    .Select(option => option.Id)
                    .ToList();
            }
        }
    }

    public sealed class AnswerOption
    {
        public string Id { get; set; } = string.Empty;
        public string TestTaskId { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
    }

    public sealed class LanguageModel
    {
        public string Key { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int Priority { get; set; }
        public int MaxConcurrentRequests { get; set; }
    }

    public sealed class SubmissionReview
    {
        public int Id { get; set; }
        public string TestId { get; set; } = string.Empty;
        public string TestTitle { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public SubmissionReviewStatus Status { get; set; } = SubmissionReviewStatus.Checked;
        public string ModelKey { get; set; } = string.Empty;
        public DateTimeOffset SubmittedAt { get; set; }
        public decimal Score { get; set; }
        public decimal MaxScore { get; set; }
        public string Summary { get; set; } = string.Empty;
        public List<TaskReviewResult> TaskResults { get; set; } = new();
    }

    public sealed class TaskReviewResult
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public int Id { get; set; }
        public int SubmissionReviewId { get; set; }
        public string TaskId { get; set; } = string.Empty;
        public string TaskTitle { get; set; } = string.Empty;
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

    public sealed class UserAccount
    {
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }
}

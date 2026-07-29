namespace LLMTutorRoom.Models
{
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
        public string Status { get; set; } = string.Empty;
        public string LlmModelKey { get; set; } = string.Empty;
        public DateTimeOffset Deadline { get; set; }
        public string Summary { get; set; } = string.Empty;
        public List<TestTask> Tasks { get; set; } = new();
        public List<RubricCriterion> Criteria { get; set; } = new();
        public List<ReferenceAnswer> ReferenceAnswers { get; set; } = new();

        public decimal TotalPoints
        {
            get
            {
                return Tasks.Sum(t => t.MaxPoints);
            }
        }
    }

    public sealed class TestTask
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
        public decimal MaxPoints { get; set; }
        public List<string> Keywords { get; set; } = new();
    }

    public sealed class RubricCriterion
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal MaxPoints { get; set; }
    }

    public sealed class ReferenceAnswer
    {
        public string Id { get; set; } = string.Empty;
        public string TaskId { get; set; } = string.Empty;
        public string StudentAlias { get; set; } = string.Empty;
        public decimal Score { get; set; }
        public string Comment { get; set; } = string.Empty;
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
        public string Status { get; set; } = string.Empty;
        public string ModelKey { get; set; } = string.Empty;
        public DateTimeOffset SubmittedAt { get; set; }
        public decimal Score { get; set; }
        public decimal MaxScore { get; set; }
        public string Summary { get; set; } = string.Empty;
        public List<TaskReviewResult> TaskResults { get; set; } = new();
    }

    public sealed class TaskReviewResult
    {
        public string TaskId { get; set; } = string.Empty;
        public string TaskTitle { get; set; } = string.Empty;
        public decimal Score { get; set; }
        public decimal MaxScore { get; set; }
        public string Feedback { get; set; } = string.Empty;
        public List<string> Findings { get; set; } = new();
    }
}

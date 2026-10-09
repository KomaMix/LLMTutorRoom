using AttemptService.Contracts.Responses;
using ReviewService.Contracts.Responses;
using TeachingService.Contracts.Enums;
using TeachingService.Contracts.Models;
using TeachingService.Contracts.Responses;

namespace LLMTutorRoom.Models;

public class TeacherClassroomOverviewResponse
{
    public List<CourseTestDto> Tests { get; set; } = new();
    public List<StudyGroupOverviewResponse> Groups { get; set; } = new();
    public List<LanguageModel> Models { get; set; } = new();
    public List<ReviewResponse> Reviews { get; set; } = new();
    public List<TestAttemptResponse> Attempts { get; set; } = new();
    public DashboardMetrics Metrics { get; set; } = new();
    public string? TerminalReviewsNextCursor { get; set; }
}

public class StudyGroupOverviewResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TeacherUserId { get; set; } = string.Empty;
    public IList<string> StudentUserIds { get; set; } = new List<string>();
    public IList<TestAssignmentResponse> Assignments { get; set; } = new List<TestAssignmentResponse>();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int Revision { get; set; }
}

public class StudentClassroomOverviewResponse
{
    public List<StudentCourseTestResponse> Tests { get; set; } = new();
    public List<LanguageModel> Models { get; set; } = new();
    public List<ReviewResponse> Reviews { get; set; } = new();
    public List<TestAttemptResponse> Attempts { get; set; } = new();
    public DashboardMetrics Metrics { get; set; } = new();
    public string? TerminalReviewsNextCursor { get; set; }
}

public class StudentCourseTestResponse
{
    public string Id { get; set; } = string.Empty;
    public Guid AssignmentId { get; set; }
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string TestId { get; set; } = string.Empty;
    public string TeacherUserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public CourseTestStatus Status { get; set; }
    public DateTimeOffset AvailableFrom { get; set; }
    public DateTimeOffset Deadline { get; set; }
    public int TimeLimitMinutes { get; set; }
    public string Summary { get; set; } = string.Empty;
    public int? LlmModelId { get; set; }
    public int VersionNumber { get; set; }
    public decimal TotalPoints { get; set; }
    public List<StudentTestTaskResponse> Tasks { get; set; } = new();
}

public class StudentTestTaskResponse
{
    public string Id { get; set; } = string.Empty;
    public TestTaskType Type { get; set; }
    public TestTaskCheckMode CheckMode { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public decimal MaxPoints { get; set; }
    public decimal WrongAnswerPenalty { get; set; }
    public bool IsHidden { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<StudentAnswerOptionResponse> Options { get; set; } = new();
}

public class StudentAnswerOptionResponse
{
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public class ReviewHistoryPageResponse
{
    public List<ReviewResponse> Reviews { get; set; } = new();
    public string? NextCursor { get; set; }
}

public class DashboardMetrics
{
    public int ActiveTests { get; set; }
    public int Tasks { get; set; }
    public int PendingReviews { get; set; }
    public decimal AverageScore { get; set; }
    public int Students { get; set; }
    public int InProgressAttempts { get; set; }
    public int SubmittedAttempts { get; set; }
}

public class LanguageModel
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

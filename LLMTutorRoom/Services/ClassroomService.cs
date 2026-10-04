using AttemptService.Contracts.Enums;
using AttemptService.Contracts.Responses;
using LLMTutorRoom.Interfaces;
using LLMTutorRoom.Models;
using LLMTutorRoom.Services.Reviews;
using ReviewService.Contracts.Responses;
using TeachingService.Contracts.Enums;
using TeachingService.Contracts.Models;
using TeachingService.Contracts.Responses;
using ReviewTeacherModelAccessResponse = ReviewService.Contracts.Responses.TeacherModelAccessResponse;

namespace LLMTutorRoom.Services;

public class ClassroomService
{
    private readonly IAttemptServiceClient _attemptServiceClient;
    private readonly ITeachingServiceClient _teachingServiceClient;
    private readonly IReviewServiceClient _reviewServiceClient;

    public ClassroomService(
        IAttemptServiceClient attemptServiceClient,
        ITeachingServiceClient teachingServiceClient,
        IReviewServiceClient reviewServiceClient)
    {
        _attemptServiceClient = attemptServiceClient;
        _teachingServiceClient = teachingServiceClient;
        _reviewServiceClient = reviewServiceClient;
    }

    public async Task<TeacherClassroomOverviewResponse> GetTeacherOverviewAsync(
        string teacherUserId,
        CancellationToken cancellationToken)
    {
        var tests = await _teachingServiceClient.GetTestsAsync(
            publishedOnly: false,
            includeHidden: true,
            cancellationToken);
        tests = tests
            .Where(test => test.TeacherUserId == teacherUserId)
            .OrderBy(test => test.Title)
            .ToList();
        var groups = await _teachingServiceClient.GetTeacherGroupsAsync(
            teacherUserId,
            cancellationToken);
        var attempts = await _attemptServiceClient.GetTeacherAttemptsAsync(
            teacherUserId,
            cancellationToken);
        var reviewPage = await _reviewServiceClient.GetTeacherReviewsAsync(
            teacherUserId,
            cancellationToken);
        var modelAccess = await _reviewServiceClient.GetTeacherModelAccessAsync(
            teacherUserId,
            includeDisabled: false,
            cancellationToken);

        return new TeacherClassroomOverviewResponse
        {
            Tests = tests,
            Groups = groups,
            Models = modelAccess.Select(ToLanguageModel).ToList(),
            Reviews = MergeReviewPage(reviewPage),
            Attempts = attempts,
            Metrics = CreateTeacherMetrics(groups, attempts, reviewPage.Aggregate),
            TerminalReviewsNextCursor = reviewPage.NextCursor
        };
    }

    public async Task<StudentClassroomOverviewResponse> GetStudentOverviewAsync(
        string studentUserId,
        CancellationToken cancellationToken)
    {
        var attempts = await _attemptServiceClient.GetStudentAttemptsAsync(
            studentUserId,
            cancellationToken);
        var assignments = await _teachingServiceClient.GetStudentAssignmentsAsync(
            studentUserId,
            cancellationToken);
        assignments = await BindAssignmentsToAttemptsAsync(
            assignments,
            attempts,
            cancellationToken);
        var studentTests = assignments
            .Select(CreateStudentTestResponse)
            .OrderBy(test => test.Deadline)
            .ToList();
        var reviewPage = await _reviewServiceClient.GetStudentReviewsAsync(
            studentUserId,
            cancellationToken);

        return new StudentClassroomOverviewResponse
        {
            Tests = studentTests,
            Models = new List<LanguageModel>(),
            Reviews = MergeReviewPage(reviewPage),
            Attempts = attempts,
            Metrics = CreateStudentMetrics(studentTests, reviewPage.Aggregate),
            TerminalReviewsNextCursor = reviewPage.NextCursor
        };
    }

    public async Task<ReviewServiceResult<ReviewHistoryPageResponse>> GetReviewHistoryAsync(
        string userId,
        bool isTeacher,
        bool includeHistoricalVersions,
        int pageSize,
        string? cursor,
        CancellationToken cancellationToken)
    {
        var result = isTeacher
            ? await _reviewServiceClient.GetTeacherReviewHistoryAsync(
                userId,
                includeHistoricalVersions,
                pageSize,
                cursor,
                cancellationToken)
            : await _reviewServiceClient.GetStudentReviewHistoryAsync(
                userId,
                pageSize,
                cursor,
                cancellationToken);

        var page = result.Value is null
            ? null
            : new ReviewHistoryPageResponse
            {
                Reviews = result.Value.TerminalReviews.ToList(),
                NextCursor = result.Value.NextCursor
            };
        return new ReviewServiceResult<ReviewHistoryPageResponse>(
            result.StatusCode,
            page,
            result.Error);
    }

    private async Task<List<TestAssignmentResponse>> BindAssignmentsToAttemptsAsync(
        IEnumerable<TestAssignmentResponse> assignedTests,
        IEnumerable<TestAttemptResponse> attempts,
        CancellationToken cancellationToken)
    {
        var assignmentsById = assignedTests.ToDictionary(
            assignment => assignment.Id,
            assignment => assignment);

        foreach (var attempt in attempts)
        {
            if (assignmentsById.ContainsKey(attempt.AssignmentId))
            {
                continue;
            }

            var assignment = await _teachingServiceClient.GetAssignmentAsync(
                attempt.AssignmentId,
                includeHidden: false,
                cancellationToken);
            if (assignment is not null)
            {
                assignmentsById[assignment.Id] = assignment;
            }
        }

        return assignmentsById.Values.ToList();
    }

    private static DashboardMetrics CreateTeacherMetrics(
        List<StudyGroupResponse> groups,
        List<TestAttemptResponse> attempts,
        ReviewAggregateResponse reviewAggregate)
    {
        var now = DateTimeOffset.UtcNow;
        return new DashboardMetrics
        {
            ActiveTests = groups
                .SelectMany(group => group.Assignments)
                .Count(assignment => assignment.AvailableFrom <= now && assignment.Deadline > now),
            Tasks = groups
                .SelectMany(group => group.Assignments)
                .Sum(assignment => assignment.Test.Tasks.Count(task => !task.IsHidden)),
            PendingReviews = reviewAggregate.PendingReviews,
            AverageScore = reviewAggregate.AverageScorePercentage,
            Students = groups
                .SelectMany(group => group.StudentUserIds)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            InProgressAttempts = attempts.Count(attempt => attempt.Status == TestAttemptStatus.InProgress),
            SubmittedAttempts = attempts.Count(attempt => attempt.Status == TestAttemptStatus.Submitted)
        };
    }

    private static DashboardMetrics CreateStudentMetrics(
        List<StudentCourseTestResponse> tests,
        ReviewAggregateResponse reviewAggregate)
    {
        var now = DateTimeOffset.UtcNow;
        return new DashboardMetrics
        {
            ActiveTests = tests.Count(test => test.AvailableFrom <= now && test.Deadline > now),
            Tasks = tests.Sum(test => test.Tasks.Count(task => !task.IsHidden)),
            PendingReviews = reviewAggregate.PendingReviews,
            AverageScore = reviewAggregate.AverageScorePercentage
        };
    }

    private static List<ReviewResponse> MergeReviewPage(ReviewPageResponse page)
    {
        return page.NonTerminalReviews
            .Concat(page.TerminalReviews)
            .DistinctBy(review => review.Id)
            .OrderByDescending(review => review.SubmittedAt)
            .ThenByDescending(review => review.Id)
            .ToList();
    }

    private static StudentCourseTestResponse CreateStudentTestResponse(
        TestAssignmentResponse assignment)
    {
        var test = assignment.Test;
        return new StudentCourseTestResponse
        {
            Id = assignment.Id.ToString("D"),
            AssignmentId = assignment.Id,
            GroupId = assignment.GroupId,
            GroupName = assignment.GroupName,
            TestId = test.Id,
            TeacherUserId = assignment.TeacherUserId,
            Title = test.Title,
            Subject = test.Subject,
            Status = CourseTestStatus.Published,
            AvailableFrom = assignment.AvailableFrom,
            Deadline = assignment.Deadline,
            TimeLimitMinutes = test.TimeLimitMinutes,
            Summary = test.Summary,
            LlmModelId = test.LlmModelId,
            VersionNumber = test.VersionNumber,
            TotalPoints = test.TotalPoints,
            Tasks = test.Tasks.Select(task => new StudentTestTaskResponse
            {
                Id = task.Id,
                Type = task.Type,
                CheckMode = task.CheckMode,
                Title = task.Title,
                Prompt = task.Prompt,
                MaxPoints = task.MaxPoints,
                WrongAnswerPenalty = task.WrongAnswerPenalty,
                IsHidden = task.IsHidden,
                CreatedAt = task.CreatedAt,
                Options = task.Options.Select(option => new StudentAnswerOptionResponse
                {
                    Id = option.Id,
                    Text = option.Text
                }).ToList()
            }).ToList()
        };
    }

    private static LanguageModel ToLanguageModel(ReviewTeacherModelAccessResponse access)
    {
        return new LanguageModel
        {
            Id = access.ModelId,
            DisplayName = access.DisplayName,
            Status = access.RemainingChecks > 0 ? "available" : "standby",
            Priority = 0,
            MaxConcurrentRequests = 0,
            PeriodSeconds = access.PeriodSeconds,
            MaxChecks = access.MaxChecks,
            UsedChecks = access.UsedChecks,
            RemainingChecks = access.RemainingChecks,
            PeriodEndsAt = access.PeriodEndsAt
        };
    }
}

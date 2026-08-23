using AttemptService.Contracts.Responses;
using LLMTutorRoom.Interfaces;
using LLMTutorRoom.Models;
using LLMTutorRoom.Services.Reviews;
using ReviewService.Contracts.Responses;
using TeachingService.Contracts.Models;
using ReviewTeacherModelAccessResponse = ReviewService.Contracts.Responses.TeacherModelAccessResponse;

namespace LLMTutorRoom.Services
{
    public sealed class ClassroomService
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

        public async Task<ClassroomOverview> GetTeacherOverviewAsync(
            string teacherUserId,
            CancellationToken cancellationToken)
        {
            var tests = await _teachingServiceClient.GetTestsAsync(
                publishedOnly: false,
                includeHidden: true,
                cancellationToken);
            tests = tests
                .Where(test => test.TeacherUserId == teacherUserId)
                .ToList();
            var reviewPage = await _reviewServiceClient.GetTeacherReviewsAsync(
                teacherUserId,
                cancellationToken);
            var reviews = MergeReviewPage(reviewPage);
            var modelAccess = await _reviewServiceClient.GetTeacherModelAccessAsync(
                teacherUserId,
                includeDisabled: false,
                cancellationToken);

            return new ClassroomOverview
            {
                Tests = tests
                    .OrderByDescending(test => test.Deadline)
                    .ToList(),
                Models = modelAccess
                    .Select(ToLanguageModel)
                    .ToList(),
                Reviews = reviews,
                Attempts = [],
                Metrics = CreateMetrics(tests, reviewPage.Aggregate),
                TerminalReviewsNextCursor = reviewPage.NextCursor
            };
        }

        public async Task<ClassroomOverview> GetStudentOverviewAsync(
            string studentUserId,
            CancellationToken cancellationToken)
        {
            var attempts = await _attemptServiceClient.GetStudentAttemptsAsync(
                studentUserId,
                cancellationToken);
            var tests = await _teachingServiceClient.GetTestsAsync(
                publishedOnly: true,
                includeHidden: false,
                cancellationToken);
            var studentTests = await BindTestsToAttemptVersionsAsync(
                tests,
                attempts,
                cancellationToken);
            var reviewPage = await _reviewServiceClient.GetStudentReviewsAsync(
                studentUserId,
                cancellationToken);
            var reviews = MergeReviewPage(reviewPage);

            return new ClassroomOverview
            {
                Tests = studentTests,
                Models = [],
                Reviews = reviews,
                Attempts = attempts,
                Metrics = CreateMetrics(studentTests, reviewPage.Aggregate),
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
                : new ReviewHistoryPageResponse(
                    result.Value.TerminalReviews.ToList(),
                    result.Value.NextCursor);
            return new ReviewServiceResult<ReviewHistoryPageResponse>(
                result.StatusCode,
                page,
                result.Error);
        }

        private async Task<List<CourseTestDto>> BindTestsToAttemptVersionsAsync(
            IEnumerable<CourseTestDto> publishedTests,
            IEnumerable<TestAttemptResponse> attempts,
            CancellationToken cancellationToken)
        {
            var testsById = publishedTests
                .GroupBy(test => test.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderByDescending(test => test.VersionNumber).First(),
                    StringComparer.OrdinalIgnoreCase);

            foreach (var attempt in attempts)
            {
                if (testsById.TryGetValue(attempt.TestId, out var publishedTest)
                    && publishedTest.VersionNumber == attempt.TestRevision)
                {
                    continue;
                }

                var exactTestVersion = await _teachingServiceClient.GetTestAsync(
                    attempt.TestId,
                    includeHidden: false,
                    versionNumber: attempt.TestRevision,
                    cancellationToken);

                if (exactTestVersion is null)
                {
                    testsById.Remove(attempt.TestId);
                    continue;
                }

                testsById[attempt.TestId] = exactTestVersion;
            }

            return testsById.Values
                .Select(CreateStudentTestResponse)
                .OrderBy(test => test.Deadline)
                .ToList();
        }

        private static DashboardMetrics CreateMetrics(
            List<CourseTestDto> tests,
            ReviewAggregateResponse reviewAggregate)
        {
            return new DashboardMetrics
            {
                ActiveTests = tests.Count(test =>
                    test.Status == TeachingService.Contracts.Enums.CourseTestStatus.Published
                    || test.PublishedVersionNumber.HasValue),
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

        private static CourseTestDto CreateStudentTestResponse(CourseTestDto test)
        {
            return new CourseTestDto
            {
                Id = test.Id,
                TeacherUserId = test.TeacherUserId,
                Title = test.Title,
                Subject = test.Subject,
                Status = test.Status,
                Deadline = test.Deadline,
                TimeLimitMinutes = test.TimeLimitMinutes,
                Summary = test.Summary,
                LlmModelKey = test.LlmModelKey,
                VersionNumber = test.VersionNumber,
                TotalPoints = test.TotalPoints,
                Tasks = test.Tasks.Select(task => new TestTaskDto
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
                    Options = task.Options.Select(option => new AnswerOptionDto
                    {
                        Id = option.Id,
                        Text = option.Text,
                        IsCorrect = false
                    }).ToList(),
                    CorrectOptionIds = []
                }).ToList()
            };
        }

        private static LanguageModel ToLanguageModel(ReviewTeacherModelAccessResponse access)
        {
            return new LanguageModel
            {
                Key = access.ModelKey,
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
}

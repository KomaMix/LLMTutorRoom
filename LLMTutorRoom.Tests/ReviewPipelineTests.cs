using System.Net;
using AttemptService.Contracts.Enums;
using AttemptService.Contracts.Responses;
using LLMTutorRoom.Interfaces;
using LLMTutorRoom.Services;
using LLMTutorRoom.Services.Reviews;
using ReviewService.Contracts.Enums;
using ReviewService.Contracts.Requests;
using ReviewService.Contracts.Responses;
using TeachingService.Contracts.Enums;
using TeachingService.Contracts.Models;
using ManualTaskReviewRequest = LLMTutorRoom.DTOs.ManualTaskReviewRequest;

namespace LLMTutorRoom.Tests;

public sealed class ReviewPipelineTests
{
    [Fact]
    public async Task GetStudentOverview_RedactsCorrectAnswersAndUsesReviewFacade()
    {
        var test = CreateTest(
            versionNumber: 3,
            CreateSingleChoiceTask());
        var review = CreateReview(test);
        var reviewClient = new FakeReviewServiceClient
        {
            StudentReviews = [review]
        };
        var service = CreateClassroomService(reviewClient, new FakeAttemptServiceClient(), test);

        var overview = await service.GetStudentOverviewAsync(
            "student",
            CancellationToken.None);

        var task = Assert.Single(Assert.Single(overview.Tests).Tasks);
        Assert.All(task.Options, option => Assert.False(option.IsCorrect));
        Assert.Empty(task.CorrectOptionIds);
        Assert.Same(review, Assert.Single(overview.Reviews));
    }

    [Fact]
    public async Task GetStudentOverview_LoadsAttemptsForRequestedStudent()
    {
        var test = CreateTest(versionNumber: 3);
        var attempt = CreateAttempt(test.Id, test.VersionNumber, TestAttemptStatus.InProgress);
        var attemptClient = new FakeAttemptServiceClient
        {
            Attempts = [attempt]
        };
        var service = CreateClassroomService(
            new FakeReviewServiceClient(),
            attemptClient,
            test);

        var overview = await service.GetStudentOverviewAsync(
            "student-id",
            CancellationToken.None);

        Assert.Equal("student-id", attemptClient.LastStudentUserId);
        Assert.Same(attempt, Assert.Single(overview.Attempts));
    }

    [Theory]
    [InlineData(TestAttemptStatus.InProgress)]
    [InlineData(TestAttemptStatus.Submitted)]
    public async Task GetStudentOverview_UsesExactVersionCapturedByAttempt(
        TestAttemptStatus attemptStatus)
    {
        var oldTask = CreateSingleChoiceTask();
        oldTask.Id = "task-v3";
        oldTask.Title = "Version 3 task";
        var currentTask = CreateSingleChoiceTask();
        currentTask.Id = "task-v4";
        currentTask.Title = "Version 4 task";
        var oldVersion = CreateTestVersion(
            versionNumber: 3,
            status: CourseTestStatus.Superseded,
            title: "Version 3",
            oldTask);
        var currentVersion = CreateTestVersion(
            versionNumber: 4,
            status: CourseTestStatus.Published,
            title: "Version 4",
            currentTask);
        var teachingClient = new FakeTeachingServiceClient([oldVersion, currentVersion]);
        var attemptClient = new FakeAttemptServiceClient
        {
            Attempts = [CreateAttempt(oldVersion.Id, 3, attemptStatus)]
        };
        var service = new ClassroomService(
            attemptClient,
            teachingClient,
            new FakeReviewServiceClient());

        var overview = await service.GetStudentOverviewAsync(
            "student",
            CancellationToken.None);

        var returnedTest = Assert.Single(overview.Tests);
        Assert.Equal(3, returnedTest.VersionNumber);
        Assert.Equal("Version 3", returnedTest.Title);
        Assert.Equal("task-v3", Assert.Single(returnedTest.Tasks).Id);
        Assert.DoesNotContain(returnedTest.Tasks, task => task.Id == "task-v4");
        Assert.Contains(
            teachingClient.TestRequests,
            request => request.TestId == oldVersion.Id && request.VersionNumber == 3);
    }

    [Fact]
    public async Task GetStudentOverview_WhenCapturedVersionIsUnavailable_DoesNotSubstituteCurrentTasks()
    {
        var currentTask = CreateSingleChoiceTask();
        currentTask.Id = "task-v4";
        var currentVersion = CreateTestVersion(
            versionNumber: 4,
            status: CourseTestStatus.Published,
            title: "Version 4",
            currentTask);
        var attemptClient = new FakeAttemptServiceClient
        {
            Attempts = [CreateAttempt(currentVersion.Id, 3, TestAttemptStatus.InProgress)]
        };
        var service = CreateClassroomService(
            new FakeReviewServiceClient(),
            attemptClient,
            currentVersion);

        var overview = await service.GetStudentOverviewAsync(
            "student",
            CancellationToken.None);

        Assert.Empty(overview.Tests);
        Assert.Single(overview.Attempts);
    }

    [Fact]
    public async Task GetTeacherOverview_PreservesCorrectAnswers()
    {
        var test = CreateTest(
            versionNumber: 3,
            CreateSingleChoiceTask());
        var service = CreateClassroomService(test);

        var overview = await service.GetTeacherOverviewAsync(
            "teacher",
            CancellationToken.None);

        var task = Assert.Single(Assert.Single(overview.Tests).Tasks);
        Assert.Contains(task.Options, option => option.IsCorrect);
        Assert.Equal(["option-correct"], task.CorrectOptionIds);
    }

    [Fact]
    public async Task GetTeacherOverview_UsesFullAggregateInsteadOfTerminalPageForMetrics()
    {
        var test = CreateTest(versionNumber: 3, CreateSingleChoiceTask());
        var reviewClient = new FakeReviewServiceClient
        {
            TeacherPage = new ReviewPageResponse(
                [],
                [],
                "next-page",
                new ReviewAggregateResponse(
                    PendingReviews: 7,
                    AverageScorePercentage: 83.4m))
        };
        var service = CreateClassroomService(
            reviewClient,
            new FakeAttemptServiceClient(),
            test);

        var overview = await service.GetTeacherOverviewAsync(
            "teacher",
            CancellationToken.None);

        Assert.Empty(overview.Reviews);
        Assert.Equal("next-page", overview.TerminalReviewsNextCursor);
        Assert.Equal(7, overview.Metrics.PendingReviews);
        Assert.Equal(83.4m, overview.Metrics.AverageScore);
    }

    [Fact]
    public async Task UpdateManualTaskReview_ForwardsLoggedInTeacherId()
    {
        var test = CreateTest(versionNumber: 1);
        var reviewClient = new FakeReviewServiceClient
        {
            ManualReviewResult = new ReviewServiceResult<ReviewResponse>(
                HttpStatusCode.OK,
                CreateReview(test),
                string.Empty)
        };
        var service = CreateClassroomService(
            reviewClient,
            new FakeAttemptServiceClient(),
            test);

        var result = await service.UpdateManualTaskReviewAsync(
            reviewId: 12,
            taskId: "task-free",
            teacherUserId: "teacher-owner",
            new ManualTaskReviewRequest
            {
                Score = 2,
                Feedback = "Хорошо",
                Findings = ["Аргументировано"]
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("teacher-owner", reviewClient.LastManualTeacherUserId);
        Assert.Equal(12, reviewClient.LastManualReviewId);
        Assert.Equal("task-free", reviewClient.LastManualTaskId);
    }

    private static ClassroomService CreateClassroomService(params CourseTestDto[] tests)
    {
        return CreateClassroomService(
            new FakeReviewServiceClient(),
            new FakeAttemptServiceClient(),
            tests);
    }

    private static ClassroomService CreateClassroomService(
        FakeReviewServiceClient reviewServiceClient,
        FakeAttemptServiceClient attemptServiceClient,
        params CourseTestDto[] tests)
    {
        return new ClassroomService(
            attemptServiceClient,
            new FakeTeachingServiceClient(tests),
            reviewServiceClient);
    }

    private static TestAttemptResponse CreateAttempt(
        string testId,
        int testRevision,
        TestAttemptStatus status)
    {
        return new TestAttemptResponse
        {
            Id = Guid.NewGuid(),
            TestId = testId,
            TestRevision = testRevision,
            Status = status,
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            EndsAt = DateTimeOffset.UtcNow.AddMinutes(40),
            SubmittedAt = status == TestAttemptStatus.Submitted
                ? DateTimeOffset.UtcNow
                : null,
            Answers = []
        };
    }

    private static CourseTestDto CreateTest(
        int versionNumber,
        params TestTaskDto[] tasks)
    {
        return CreateTestVersion(
            versionNumber,
            CourseTestStatus.Published,
            "Test",
            tasks);
    }

    private static CourseTestDto CreateTestVersion(
        int versionNumber,
        CourseTestStatus status,
        string title,
        params TestTaskDto[] tasks)
    {
        return new CourseTestDto
        {
            Id = "test-1",
            TeacherUserId = "teacher",
            Title = title,
            Subject = "Subject",
            Status = status,
            Deadline = DateTimeOffset.UtcNow.AddDays(1),
            TimeLimitMinutes = 45,
            LlmModelKey = "gemma3:12b",
            VersionNumber = versionNumber,
            TotalPoints = tasks.Sum(task => task.MaxPoints),
            Tasks = tasks.ToList()
        };
    }

    private static TestTaskDto CreateSingleChoiceTask()
    {
        return new TestTaskDto
        {
            Id = "task-choice",
            Type = TestTaskType.SingleChoice,
            CheckMode = TestTaskCheckMode.Auto,
            Title = "Choice",
            Prompt = "Choose.",
            MaxPoints = 2,
            CreatedAt = DateTimeOffset.UtcNow,
            CorrectOptionIds = ["option-correct"],
            Options =
            [
                new AnswerOptionDto
                {
                    Id = "option-correct",
                    Text = "Correct",
                    IsCorrect = true
                },
                new AnswerOptionDto
                {
                    Id = "option-wrong",
                    Text = "Wrong",
                    IsCorrect = false
                }
            ]
        };
    }

    private static ReviewResponse CreateReview(CourseTestDto test)
    {
        return new ReviewResponse(
            Id: 1,
            AttemptId: Guid.NewGuid(),
            TestId: test.Id,
            TestRevision: test.VersionNumber,
            TestTitle: test.Title,
            TeacherUserId: test.TeacherUserId,
            StudentUserId: "student",
            StudentName: "Student",
            Status: ReviewStatus.ManualReview,
            ModelKey: test.LlmModelKey,
            SubmittedAt: DateTimeOffset.UtcNow,
            QueuedAt: null,
            StartedAt: null,
            CompletedAt: null,
            NextRetryAt: null,
            ProcessingAttempts: 0,
            LastError: string.Empty,
            Score: 0,
            MaxScore: test.TotalPoints,
            Summary: string.Empty,
            TaskResults: []);
    }

    private sealed class FakeAttemptServiceClient : IAttemptServiceClient
    {
        public List<TestAttemptResponse> Attempts { get; init; } = [];
        public string? LastStudentUserId { get; private set; }

        public Task<List<TestAttemptResponse>> GetStudentAttemptsAsync(
            string studentUserId,
            CancellationToken cancellationToken)
        {
            LastStudentUserId = studentUserId;
            return Task.FromResult(Attempts);
        }
    }

    private sealed class FakeTeachingServiceClient(IEnumerable<CourseTestDto> tests)
        : ITeachingServiceClient
    {
        private readonly List<CourseTestDto> _tests = tests.ToList();

        public List<(string TestId, int? VersionNumber)> TestRequests { get; } = [];

        public Task<List<CourseTestDto>> GetTestsAsync(
            bool publishedOnly,
            bool includeHidden,
            CancellationToken cancellationToken)
        {
            var tests = _tests.AsEnumerable();
            if (publishedOnly)
                tests = tests.Where(test => test.Status == CourseTestStatus.Published);

            return Task.FromResult(tests.ToList());
        }

        public Task<CourseTestDto?> GetTestAsync(
            string testId,
            bool includeHidden,
            int? versionNumber,
            CancellationToken cancellationToken)
        {
            TestRequests.Add((testId, versionNumber));
            var matchingTests = _tests.Where(test => test.Id == testId);
            var test = versionNumber.HasValue
                ? matchingTests.SingleOrDefault(test => test.VersionNumber == versionNumber.Value)
                : matchingTests
                    .Where(test => test.Status == CourseTestStatus.Published)
                    .OrderByDescending(test => test.VersionNumber)
                    .FirstOrDefault()
                    ?? matchingTests
                        .Where(test => test.Status == CourseTestStatus.Superseded)
                        .OrderByDescending(test => test.VersionNumber)
                        .FirstOrDefault();

            return Task.FromResult(test);
        }
    }

    private sealed class FakeReviewServiceClient : IReviewServiceClient
    {
        public ReviewPageResponse? TeacherPage { get; init; }
        public ReviewPageResponse? StudentPage { get; init; }
        public List<ReviewResponse> TeacherReviews { get; init; } = [];
        public List<ReviewResponse> StudentReviews { get; init; } = [];
        public ReviewServiceResult<ReviewResponse> ManualReviewResult { get; init; }
            = new(HttpStatusCode.NotFound, null, string.Empty);

        public int? LastManualReviewId { get; private set; }
        public string? LastManualTaskId { get; private set; }
        public string? LastManualTeacherUserId { get; private set; }

        public Task<ReviewPageResponse> GetTeacherReviewsAsync(
            string teacherUserId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(TeacherPage ?? CreatePage(TeacherReviews));
        }

        public Task<ReviewPageResponse> GetStudentReviewsAsync(
            string studentUserId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(StudentPage ?? CreatePage(StudentReviews));
        }

        public Task<ReviewServiceResult<ReviewPageResponse>> GetTeacherReviewHistoryAsync(
            string teacherUserId,
            bool includeHistoricalVersions,
            int pageSize,
            string? cursor,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new ReviewServiceResult<ReviewPageResponse>(
                HttpStatusCode.OK,
                CreatePage(TeacherReviews, includeNonTerminal: false),
                string.Empty));
        }

        public Task<ReviewServiceResult<ReviewPageResponse>> GetStudentReviewHistoryAsync(
            string studentUserId,
            int pageSize,
            string? cursor,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new ReviewServiceResult<ReviewPageResponse>(
                HttpStatusCode.OK,
                CreatePage(StudentReviews, includeNonTerminal: false),
                string.Empty));
        }

        public Task<ReviewServiceResult<ReviewResponse>> UpdateManualTaskReviewAsync(
            int reviewId,
            string taskId,
            string teacherUserId,
            UpdateManualTaskReviewRequest request,
            CancellationToken cancellationToken)
        {
            LastManualReviewId = reviewId;
            LastManualTaskId = taskId;
            LastManualTeacherUserId = teacherUserId;
            return Task.FromResult(ManualReviewResult);
        }

        public Task<List<LlmModelCatalogItemResponse>> GetModelCatalogAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult<List<LlmModelCatalogItemResponse>>([]);
        }

        public Task<List<TeacherModelAccessResponse>> GetTeacherModelAccessAsync(
            string teacherUserId,
            bool includeDisabled,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<List<TeacherModelAccessResponse>>([]);
        }

        public Task<ReviewServiceResult<TeacherModelAccessResponse>> UpsertTeacherModelAccessAsync(
            string teacherUserId,
            string modelKey,
            UpsertTeacherModelAccessRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new ReviewServiceResult<TeacherModelAccessResponse>(
                HttpStatusCode.NotFound,
                null,
                string.Empty));
        }

        public Task<ReviewServiceResult<object>> DeleteTeacherModelAccessAsync(
            string teacherUserId,
            string modelKey,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new ReviewServiceResult<object>(
                HttpStatusCode.NotFound,
                null,
                string.Empty));
        }

        private static ReviewPageResponse CreatePage(
            List<ReviewResponse> reviews,
            bool includeNonTerminal = true)
        {
            var nonTerminal = reviews
                .Where(review => review.Status is not ReviewStatus.Checked
                    and not ReviewStatus.Failed)
                .ToList();
            var terminal = reviews
                .Where(review => review.Status is ReviewStatus.Checked
                    or ReviewStatus.Failed)
                .ToList();
            var percentages = terminal
                .Where(review => review.Status == ReviewStatus.Checked && review.MaxScore > 0)
                .Select(review => review.Score / review.MaxScore * 100)
                .ToList();
            return new ReviewPageResponse(
                includeNonTerminal ? nonTerminal : [],
                terminal,
                null,
                new ReviewAggregateResponse(
                    includeNonTerminal ? nonTerminal.Count : 0,
                    percentages.Count == 0 ? 0 : Math.Round(percentages.Average(), 1)));
        }
    }
}

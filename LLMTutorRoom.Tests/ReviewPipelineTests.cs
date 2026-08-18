using System.Net;
using System.Text.Json;
using LLMTutorRoom.Data;
using LLMTutorRoom.Enums;
using LLMTutorRoom.Interfaces;
using LLMTutorRoom.Models;
using LLMTutorRoom.Services;
using LLMTutorRoom.Services.Reviews;
using LLMTutorRoom.Services.Teaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ReviewService.Contracts.Enums;
using ReviewService.Contracts.Events;
using ReviewService.Contracts.Requests;
using ReviewService.Contracts.Responses;
using TeachingService.Contracts.Enums;
using TeachingService.Contracts.Models;
using ManualTaskReviewRequest = LLMTutorRoom.DTOs.ManualTaskReviewRequest;

namespace LLMTutorRoom.Tests
{
    public sealed class ReviewPipelineTests
    {
        [Fact]
        public async Task StartAttempt_CopiesPublishedVersionNumber()
        {
            await using var dbContext = CreateDbContext();
            var test = CreateTestVersion(
                versionNumber: 7,
                status: CourseTestStatus.Published,
                title: "Version 7");
            var service = CreateClassroomService(dbContext, test);

            var attempt = await service.StartAttemptAsync(
                test.Id,
                "student",
                CancellationToken.None);

            Assert.NotNull(attempt);
            var storedAttempt = await dbContext.TestAttempts.SingleAsync();
            Assert.Equal(7, storedAttempt.TestRevision);
        }

        [Fact]
        public async Task StartAttempt_WhenDisplayedVersionIsNoLongerPublished_ReturnsConflictWithoutCreatingAttempt()
        {
            await using var dbContext = CreateDbContext();
            var publishedVersion = CreateTestVersion(
                versionNumber: 4,
                status: CourseTestStatus.Published,
                title: "Version 4");
            var service = CreateClassroomService(dbContext, publishedVersion);

            var result = await service.StartAttemptAsync(
                publishedVersion.Id,
                "student",
                expectedVersionNumber: 3,
                CancellationToken.None);

            Assert.Equal(StartAttemptOutcome.VersionConflict, result.Outcome);
            Assert.Null(result.Attempt);
            Assert.Empty(dbContext.TestAttempts);
        }

        [Fact]
        public async Task StartAttempt_WhenNewVersionIsPublished_ReturnsExistingLogicalTestAttempt()
        {
            await using var dbContext = CreateDbContext();
            var existingAttempt = await AddAttemptAsync(
                dbContext,
                testId: "test-1",
                testRevision: 3,
                new Dictionary<string, string>());
            var publishedVersion = CreateTestVersion(
                versionNumber: 4,
                status: CourseTestStatus.Published,
                title: "Version 4");
            var service = CreateClassroomService(dbContext, publishedVersion);

            var returnedAttempt = await service.StartAttemptAsync(
                publishedVersion.Id,
                "student",
                CancellationToken.None);

            Assert.NotNull(returnedAttempt);
            Assert.Equal(existingAttempt.Id, returnedAttempt.Id);
            Assert.Equal(3, returnedAttempt.TestRevision);
            Assert.Equal(1, await dbContext.TestAttempts.CountAsync());
            Assert.Equal(3, (await dbContext.TestAttempts.SingleAsync()).TestRevision);
        }

        [Fact]
        public async Task SaveAttemptAnswers_UsesTaskIdsCapturedAtAttemptStart()
        {
            await using var dbContext = CreateDbContext();
            var task = CreateSingleChoiceTask();
            var test = CreateTest(versionNumber: 7, task);
            var service = CreateClassroomService(dbContext, test);
            var started = await service.StartAttemptAsync(
                test.Id,
                "student",
                CancellationToken.None);
            Assert.NotNull(started);

            test.Tasks.Clear();
            var saved = await service.SaveAttemptAnswersAsync(
                started.Id,
                "student",
                new Dictionary<string, string>
                {
                    [task.Id] = "option-correct",
                    ["not-in-revision"] = "ignored"
                },
                CancellationToken.None);

            Assert.NotNull(saved);
            Assert.Equal("option-correct", saved.Answers[task.Id]);
            Assert.DoesNotContain("not-in-revision", saved.Answers.Keys);
        }

        [Fact]
        public async Task SubmitAttempt_CreatesOutboxFact()
        {
            await using var dbContext = CreateDbContext();
            var test = CreateTest(versionNumber: 4);
            var attempt = await AddAttemptAsync(
                dbContext,
                test.Id,
                test.VersionNumber,
                new Dictionary<string, string> { ["task-free"] = "Ответ ученика." });
            var service = CreateClassroomService(dbContext, test);

            var submitted = await service.SubmitAttemptAsync(
                attempt.Id,
                "student",
                "Student Name",
                CancellationToken.None);

            Assert.NotNull(submitted);
            Assert.Equal(TestAttemptStatus.Submitted, submitted.Status);
            var outbox = await dbContext.AttemptSubmissionOutboxMessages.SingleAsync();
            var integrationEvent = JsonSerializer.Deserialize<AttemptSubmittedV1>(
                outbox.PayloadJson,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));

            Assert.NotNull(integrationEvent);
            Assert.Equal(outbox.Id, integrationEvent.EventId);
            Assert.Equal(attempt.Id, integrationEvent.AttemptId);
            Assert.Equal(test.Id, integrationEvent.TestId);
            Assert.Equal(test.VersionNumber, integrationEvent.TestRevision);
            Assert.Equal("student", integrationEvent.StudentUserId);
            Assert.Equal("Student Name", integrationEvent.StudentName);
            Assert.Equal("Ответ ученика.", integrationEvent.Answers["task-free"]);
            Assert.Null(outbox.PublishedAt);
        }

        [Fact]
        public async Task SubmitAttempt_WhenTrackedAnswersBecomeStale_ReloadsBeforeCreatingOutbox()
        {
            var databaseRoot = new InMemoryDatabaseRoot();
            var databaseName = $"llmtutorroom-concurrency-{Guid.NewGuid():N}";
            var test = CreateTest(versionNumber: 4);

            int attemptId;
            await using (var seedContext = CreateDbContext(databaseName, databaseRoot))
            {
                var seededAttempt = await AddAttemptAsync(
                    seedContext,
                    test.Id,
                    test.VersionNumber,
                    new Dictionary<string, string> { ["task-free"] = "Старый ответ" });
                attemptId = seededAttempt.Id;
            }

            await using var serviceContext = CreateDbContext(databaseName, databaseRoot);
            _ = await serviceContext.TestAttempts.SingleAsync();

            await using (var concurrentContext = CreateDbContext(databaseName, databaseRoot))
            {
                var concurrentAttempt = await concurrentContext.TestAttempts.SingleAsync();
                concurrentAttempt.AnswersJson = JsonSerializer.Serialize(
                    new Dictionary<string, string> { ["task-free"] = "Последний ответ" },
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
                concurrentAttempt.StateRevision++;
                await concurrentContext.SaveChangesAsync();
            }

            var service = CreateClassroomService(serviceContext, test);
            var submitted = await service.SubmitAttemptAsync(
                attemptId,
                "student",
                "Student",
                CancellationToken.None);

            Assert.NotNull(submitted);
            Assert.Equal(TestAttemptStatus.Submitted, submitted.Status);
            Assert.Equal("Последний ответ", submitted.Answers["task-free"]);

            var storedAttempt = await serviceContext.TestAttempts.AsNoTracking().SingleAsync();
            var outbox = await serviceContext.AttemptSubmissionOutboxMessages
                .AsNoTracking()
                .SingleAsync();
            var integrationEvent = JsonSerializer.Deserialize<AttemptSubmittedV1>(
                outbox.PayloadJson,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));

            Assert.Equal(2, storedAttempt.StateRevision);
            Assert.NotNull(integrationEvent);
            Assert.Equal("Последний ответ", integrationEvent.Answers["task-free"]);
        }

        [Fact]
        public async Task SubmitAttempt_WhenRepeated_DoesNotDuplicateOutboxFact()
        {
            await using var dbContext = CreateDbContext();
            var test = CreateTest(versionNumber: 2);
            var attempt = await AddAttemptAsync(
                dbContext,
                test.Id,
                test.VersionNumber,
                new Dictionary<string, string>());
            var service = CreateClassroomService(dbContext, test);

            await service.SubmitAttemptAsync(
                attempt.Id,
                "student",
                "Student",
                CancellationToken.None);
            await service.SubmitAttemptAsync(
                attempt.Id,
                "student",
                "Student",
                CancellationToken.None);

            Assert.Equal(1, await dbContext.AttemptSubmissionOutboxMessages.CountAsync());
        }

        [Fact]
        public async Task SubmitAttempt_ForAlreadySubmittedAttempt_DoesNotEmitFact()
        {
            await using var dbContext = CreateDbContext();
            var test = CreateTest(versionNumber: 9);
            var attempt = await AddAttemptAsync(
                dbContext,
                test.Id,
                testRevision: test.VersionNumber,
                new Dictionary<string, string>(),
                TestAttemptStatus.Submitted);
            var service = CreateClassroomService(dbContext, test);

            var submitted = await service.SubmitAttemptAsync(
                attempt.Id,
                "student",
                "Student",
                CancellationToken.None);

            Assert.NotNull(submitted);
            Assert.Equal(TestAttemptStatus.Submitted, submitted.Status);
            Assert.Empty(dbContext.AttemptSubmissionOutboxMessages);
        }

        [Fact]
        public async Task GetStudentOverview_RedactsCorrectAnswersAndUsesReviewFacade()
        {
            await using var dbContext = CreateDbContext();
            var test = CreateTest(
                versionNumber: 3,
                CreateSingleChoiceTask());
            var review = CreateReview(test);
            var reviewClient = new FakeReviewServiceClient
            {
                StudentReviews = new List<ReviewResponse> { review }
            };
            var service = CreateClassroomService(dbContext, reviewClient, test);

            var overview = await service.GetStudentOverviewAsync(
                "student",
                CancellationToken.None);

            var task = Assert.Single(Assert.Single(overview.Tests).Tasks);
            Assert.All(task.Options, option => Assert.False(option.IsCorrect));
            Assert.Empty(task.CorrectOptionIds);
            Assert.Same(review, Assert.Single(overview.Reviews));
        }

        [Theory]
        [InlineData(TestAttemptStatus.InProgress)]
        [InlineData(TestAttemptStatus.Submitted)]
        public async Task GetStudentOverview_UsesExactVersionCapturedByAttempt(
            TestAttemptStatus attemptStatus)
        {
            await using var dbContext = CreateDbContext();
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
            await AddAttemptAsync(
                dbContext,
                oldVersion.Id,
                testRevision: 3,
                new Dictionary<string, string>(),
                attemptStatus);
            var teachingClient = new FakeTeachingServiceClient(
                new[] { oldVersion, currentVersion });
            var service = new ClassroomService(
                dbContext,
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
            await using var dbContext = CreateDbContext();
            var currentTask = CreateSingleChoiceTask();
            currentTask.Id = "task-v4";
            var currentVersion = CreateTestVersion(
                versionNumber: 4,
                status: CourseTestStatus.Published,
                title: "Version 4",
                currentTask);
            await AddAttemptAsync(
                dbContext,
                currentVersion.Id,
                testRevision: 3,
                new Dictionary<string, string>());
            var service = CreateClassroomService(dbContext, currentVersion);

            var overview = await service.GetStudentOverviewAsync(
                "student",
                CancellationToken.None);

            Assert.Empty(overview.Tests);
            Assert.Single(overview.Attempts);
        }

        [Fact]
        public async Task GetTeacherOverview_PreservesCorrectAnswers()
        {
            await using var dbContext = CreateDbContext();
            var test = CreateTest(
                versionNumber: 3,
                CreateSingleChoiceTask());
            var service = CreateClassroomService(dbContext, test);

            var overview = await service.GetTeacherOverviewAsync(
                "teacher",
                CancellationToken.None);

            var task = Assert.Single(Assert.Single(overview.Tests).Tasks);
            Assert.Contains(task.Options, option => option.IsCorrect);
            Assert.Equal(new[] { "option-correct" }, task.CorrectOptionIds);
        }

        [Fact]
        public async Task GetTeacherOverview_UsesFullAggregateInsteadOfTerminalPageForMetrics()
        {
            await using var dbContext = CreateDbContext();
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
            var service = CreateClassroomService(dbContext, reviewClient, test);

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
            await using var dbContext = CreateDbContext();
            var test = CreateTest(versionNumber: 1);
            var reviewClient = new FakeReviewServiceClient
            {
                ManualReviewResult = new ReviewServiceResult<ReviewResponse>(
                    HttpStatusCode.OK,
                    CreateReview(test),
                    string.Empty)
            };
            var service = CreateClassroomService(dbContext, reviewClient, test);

            var result = await service.UpdateManualTaskReviewAsync(
                reviewId: 12,
                taskId: "task-free",
                teacherUserId: "teacher-owner",
                new ManualTaskReviewRequest
                {
                    Score = 2,
                    Feedback = "Хорошо",
                    Findings = new List<string> { "Аргументировано" }
                },
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal("teacher-owner", reviewClient.LastManualTeacherUserId);
            Assert.Equal(12, reviewClient.LastManualReviewId);
            Assert.Equal("task-free", reviewClient.LastManualTaskId);
        }

        private static ClassroomService CreateClassroomService(
            TutorRoomDbContext dbContext,
            params CourseTestDto[] tests)
        {
            return CreateClassroomService(
                dbContext,
                new FakeReviewServiceClient(),
                tests);
        }

        private static ClassroomService CreateClassroomService(
            TutorRoomDbContext dbContext,
            FakeReviewServiceClient reviewServiceClient,
            params CourseTestDto[] tests)
        {
            return new ClassroomService(
                dbContext,
                new FakeTeachingServiceClient(tests),
                reviewServiceClient);
        }

        private static TutorRoomDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<TutorRoomDbContext>()
                .UseInMemoryDatabase($"llmtutorroom-tests-{Guid.NewGuid():N}")
                .Options;

            return new TutorRoomDbContext(options);
        }

        private static TutorRoomDbContext CreateDbContext(
            string databaseName,
            InMemoryDatabaseRoot databaseRoot)
        {
            var options = new DbContextOptionsBuilder<TutorRoomDbContext>()
                .UseInMemoryDatabase(databaseName, databaseRoot)
                .Options;

            return new TutorRoomDbContext(options);
        }

        private static async Task<TestAttempt> AddAttemptAsync(
            TutorRoomDbContext dbContext,
            string testId,
            int testRevision,
            Dictionary<string, string> answers,
            TestAttemptStatus status = TestAttemptStatus.InProgress)
        {
            var attempt = new TestAttempt
            {
                TestId = testId,
                TestRevision = testRevision,
                StudentUserId = "student",
                Status = status,
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                EndsAt = DateTimeOffset.UtcNow.AddMinutes(40),
                SubmittedAt = status == TestAttemptStatus.Submitted
                    ? DateTimeOffset.UtcNow
                    : null,
                AnswersJson = JsonSerializer.Serialize(
                    answers,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web))
            };

            dbContext.TestAttempts.Add(attempt);
            await dbContext.SaveChangesAsync();
            return attempt;
        }

        private static CourseTestDto CreateTest(
            int versionNumber,
            params TestTaskDto[] tasks)
        {
            return CreateTestVersion(
                versionNumber: versionNumber,
                status: CourseTestStatus.Published,
                title: "Test",
                tasks: tasks);
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
                CorrectOptionIds = new List<string> { "option-correct" },
                Options = new List<AnswerOptionDto>
                {
                    new()
                    {
                        Id = "option-correct",
                        Text = "Correct",
                        IsCorrect = true
                    },
                    new()
                    {
                        Id = "option-wrong",
                        Text = "Wrong",
                        IsCorrect = false
                    }
                }
            };
        }

        private static ReviewResponse CreateReview(CourseTestDto test)
        {
            return new ReviewResponse(
                Id: 1,
                AttemptId: 1,
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

        private sealed class FakeTeachingServiceClient : ITeachingServiceClient
        {
            private readonly List<CourseTestDto> _tests;

            public List<(string TestId, int? VersionNumber)> TestRequests { get; } = new();

            public FakeTeachingServiceClient(IEnumerable<CourseTestDto> tests)
            {
                _tests = tests.ToList();
            }

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
                var matchingTests = _tests
                    .Where(test => test.Id == testId);
                var test = versionNumber.HasValue
                    ? matchingTests.SingleOrDefault(
                        test => test.VersionNumber == versionNumber.Value)
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
}

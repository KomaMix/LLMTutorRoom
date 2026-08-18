using System.Text.Json;
using LLMTutorRoom.Data;
using LLMTutorRoom.DTOs;
using LLMTutorRoom.Enums;
using LLMTutorRoom.Interfaces;
using LLMTutorRoom.Models;
using LLMTutorRoom.Services.Reviews;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReviewService.Contracts.Enums;
using ReviewService.Contracts.Events;
using ReviewService.Contracts.Responses;
using TeachingService.Contracts.Models;
using ReviewManualTaskRequest = ReviewService.Contracts.Requests.UpdateManualTaskReviewRequest;
using ReviewTeacherModelAccessResponse = ReviewService.Contracts.Responses.TeacherModelAccessResponse;

namespace LLMTutorRoom.Services
{
    public sealed class ClassroomService
    {
        private const int MaxAttemptMutationAttempts = 5;
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly TutorRoomDbContext _dbContext;
        private readonly ITeachingServiceClient _teachingServiceClient;
        private readonly IReviewServiceClient _reviewServiceClient;

        public ClassroomService(
            TutorRoomDbContext dbContext,
            ITeachingServiceClient teachingServiceClient,
            IReviewServiceClient reviewServiceClient)
        {
            _dbContext = dbContext;
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
                Attempts = new List<TestAttemptResponse>(),
                Metrics = CreateMetrics(tests, reviewPage.Aggregate),
                TerminalReviewsNextCursor = reviewPage.NextCursor
            };
        }

        public async Task<ClassroomOverview> GetStudentOverviewAsync(
            string studentUserId,
            CancellationToken cancellationToken)
        {
            await ExpireStudentAttemptsAsync(studentUserId, cancellationToken);

            var attempts = await _dbContext.TestAttempts
                .AsNoTracking()
                .Where(attempt => attempt.StudentUserId.ToLower() == studentUserId.ToLower())
                .OrderByDescending(attempt => attempt.StartedAt)
                .ToListAsync(cancellationToken);
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
                Models = new List<LanguageModel>(),
                Reviews = reviews,
                Attempts = attempts.Select(ToAttemptResponse).ToList(),
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

        public async Task<TestAttemptResponse?> StartAttemptAsync(
            string testId,
            string studentUserId,
            CancellationToken cancellationToken)
        {
            var result = await StartAttemptAsync(
                testId,
                studentUserId,
                expectedVersionNumber: null,
                cancellationToken);
            return result.Attempt;
        }

        public async Task<StartAttemptResult> StartAttemptAsync(
            string testId,
            string studentUserId,
            int? expectedVersionNumber,
            CancellationToken cancellationToken)
        {
            await ExpireStudentAttemptsAsync(studentUserId, cancellationToken);

            var existingAttempt = await _dbContext.TestAttempts
                .SingleOrDefaultAsync(
                    attempt => attempt.TestId == testId
                        && attempt.StudentUserId.ToLower() == studentUserId.ToLower(),
                    cancellationToken);

            if (existingAttempt is not null)
            {
                return new StartAttemptResult(
                    StartAttemptOutcome.Success,
                    ToAttemptResponse(existingAttempt));
            }

            var test = await _teachingServiceClient.GetTestAsync(
                testId,
                includeHidden: false,
                versionNumber: null,
                cancellationToken);

            if (test is null || test.Status != TeachingService.Contracts.Enums.CourseTestStatus.Published)
                return new StartAttemptResult(StartAttemptOutcome.NotFound);

            var versionNumber = test.VersionNumber;
            if (versionNumber <= 0)
                return new StartAttemptResult(StartAttemptOutcome.NotFound);

            if (expectedVersionNumber.HasValue
                && expectedVersionNumber.Value != versionNumber)
            {
                return new StartAttemptResult(StartAttemptOutcome.VersionConflict);
            }

            var now = DateTimeOffset.UtcNow;
            if (test.Deadline <= now)
                return new StartAttemptResult(StartAttemptOutcome.NotFound);

            var attempt = new TestAttempt
            {
                TestId = test.Id,
                TestRevision = versionNumber,
                StudentUserId = studentUserId,
                Status = TestAttemptStatus.InProgress,
                StartedAt = now,
                EndsAt = Min(now.AddMinutes(test.TimeLimitMinutes), test.Deadline),
                AnswersJson = "{}",
                AllowedTaskIdsJson = SerializeTaskIds(test.Tasks.Select(task => task.Id))
            };

            _dbContext.TestAttempts.Add(attempt);
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsDuplicateAttempt(exception))
            {
                _dbContext.Entry(attempt).State = EntityState.Detached;
                var concurrentlyCreatedAttempt = await _dbContext.TestAttempts
                    .SingleAsync(
                        item => item.TestId == testId
                            && item.StudentUserId.ToLower() == studentUserId.ToLower(),
                        cancellationToken);
                return new StartAttemptResult(
                    StartAttemptOutcome.Success,
                    ToAttemptResponse(concurrentlyCreatedAttempt));
            }

            return new StartAttemptResult(
                StartAttemptOutcome.Success,
                ToAttemptResponse(attempt));
        }

        public async Task<TestAttemptResponse?> SaveAttemptAnswersAsync(
            int attemptId,
            string studentUserId,
            Dictionary<string, string> answers,
            CancellationToken cancellationToken)
        {
            for (var mutationAttempt = 1;
                 mutationAttempt <= MaxAttemptMutationAttempts;
                 mutationAttempt++)
            {
                var attempt = await FindStudentAttemptAsync(
                    attemptId,
                    studentUserId,
                    cancellationToken);

                if (attempt is null)
                    return null;

                var statusChanged = ExpireAttemptIfNeeded(attempt, DateTimeOffset.UtcNow);
                if (attempt.Status == TestAttemptStatus.InProgress)
                {
                    attempt.AnswersJson = SerializeAnswers(FilterAnswers(attempt, answers));
                }

                if (!statusChanged && attempt.Status != TestAttemptStatus.InProgress)
                    return ToAttemptResponse(attempt);

                IncrementAttemptStateRevision(attempt);
                try
                {
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    return ToAttemptResponse(attempt);
                }
                catch (DbUpdateConcurrencyException exception)
                {
                    _dbContext.ChangeTracker.Clear();
                    if (mutationAttempt == MaxAttemptMutationAttempts)
                    {
                        throw new AttemptWriteConflictException(
                            "The attempt changed repeatedly while answers were being saved.",
                            exception);
                    }
                }
            }

            throw new InvalidOperationException("Attempt answer retry loop terminated unexpectedly.");
        }

        public async Task<TestAttemptResponse?> SubmitAttemptAsync(
            int attemptId,
            string studentUserId,
            string studentDisplayName,
            CancellationToken cancellationToken)
        {
            for (var mutationAttempt = 1;
                 mutationAttempt <= MaxAttemptMutationAttempts;
                 mutationAttempt++)
            {
                var attempt = await FindStudentAttemptAsync(
                    attemptId,
                    studentUserId,
                    cancellationToken);

                if (attempt is null)
                    return null;

                var now = DateTimeOffset.UtcNow;
                var statusChanged = ExpireAttemptIfNeeded(attempt, now);
                var submittedNow = false;
                if (attempt.Status == TestAttemptStatus.InProgress)
                {
                    attempt.Status = TestAttemptStatus.Submitted;
                    attempt.SubmittedAt = now;
                    statusChanged = true;
                    submittedNow = true;
                }

                var outboxAdded = submittedNow
                    && await EnsureAttemptSubmittedOutboxAsync(
                        attempt,
                        studentDisplayName,
                        cancellationToken);

                if (!statusChanged && !outboxAdded)
                    return ToAttemptResponse(attempt);

                IncrementAttemptStateRevision(attempt);
                try
                {
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    return ToAttemptResponse(attempt);
                }
                catch (DbUpdateConcurrencyException exception)
                {
                    _dbContext.ChangeTracker.Clear();
                    if (mutationAttempt == MaxAttemptMutationAttempts)
                    {
                        throw new AttemptWriteConflictException(
                            "The attempt changed repeatedly while it was being submitted.",
                            exception);
                    }
                }
                catch (DbUpdateException exception) when (
                    IsDuplicateAttemptSubmissionOutbox(exception))
                {
                    _dbContext.ChangeTracker.Clear();
                    if (mutationAttempt == MaxAttemptMutationAttempts)
                    {
                        throw new AttemptWriteConflictException(
                            "The attempt was submitted concurrently.",
                            exception);
                    }
                }
            }

            throw new InvalidOperationException("Attempt submission retry loop terminated unexpectedly.");
        }

        public Task<ReviewServiceResult<ReviewResponse>> UpdateManualTaskReviewAsync(
            int reviewId,
            string taskId,
            string teacherUserId,
            ManualTaskReviewRequest request,
            CancellationToken cancellationToken)
        {
            return _reviewServiceClient.UpdateManualTaskReviewAsync(
                reviewId,
                taskId,
                teacherUserId,
                new ReviewManualTaskRequest
                {
                    Score = request.Score,
                    Feedback = request.Feedback,
                    Findings = request.Findings
                },
                cancellationToken);
        }

        private async Task<bool> EnsureAttemptSubmittedOutboxAsync(
            TestAttempt attempt,
            string studentDisplayName,
            CancellationToken cancellationToken)
        {
            var existingOutbox = await _dbContext.AttemptSubmissionOutboxMessages
                .SingleOrDefaultAsync(
                    message => message.AttemptId == attempt.Id,
                    cancellationToken);
            var eventId = existingOutbox?.Id ?? Guid.NewGuid();
            var occurredAt = attempt.SubmittedAt
                ?? throw new InvalidOperationException("Submitted attempt must have SubmittedAt.");
            var message = new AttemptSubmittedV1(
                eventId,
                attempt.Id,
                attempt.TestId,
                attempt.TestRevision,
                attempt.StudentUserId,
                string.IsNullOrWhiteSpace(studentDisplayName)
                    ? "Студент"
                    : studentDisplayName.Trim(),
                DeserializeAnswers(attempt.AnswersJson),
                occurredAt);

            var payload = JsonSerializer.Serialize(message, JsonOptions);
            if (existingOutbox is null)
            {
                _dbContext.AttemptSubmissionOutboxMessages.Add(new AttemptSubmissionOutboxMessage
                {
                    Id = eventId,
                    AttemptId = attempt.Id,
                    OccurredAt = occurredAt,
                    PayloadJson = payload
                });
            }
            else
            {
                // A relational SaveChanges transaction normally makes a submitted attempt and
                // its outbox row indivisible. Refreshing an unpublished pre-existing row keeps
                // the message aligned with the latest persisted answers.
                if (existingOutbox.PublishedAt is not null)
                {
                    throw new InvalidOperationException(
                        "A published attempt event exists for an in-progress attempt.");
                }

                existingOutbox.OccurredAt = occurredAt;
                existingOutbox.PayloadJson = payload;
                existingOutbox.PublishAttempts = 0;
                existingOutbox.NextPublishAt = null;
                existingOutbox.LastError = string.Empty;
            }

            return true;
        }

        private async Task ExpireStudentAttemptsAsync(
            string studentUserId,
            CancellationToken cancellationToken)
        {
            for (var mutationAttempt = 1;
                 mutationAttempt <= MaxAttemptMutationAttempts;
                 mutationAttempt++)
            {
                var now = DateTimeOffset.UtcNow;
                var attempts = await _dbContext.TestAttempts
                    .Where(attempt => attempt.StudentUserId.ToLower() == studentUserId.ToLower()
                        && attempt.Status == TestAttemptStatus.InProgress
                        && attempt.EndsAt <= now)
                    .ToListAsync(cancellationToken);

                if (attempts.Count == 0)
                    return;

                foreach (var attempt in attempts)
                {
                    attempt.Status = TestAttemptStatus.Expired;
                    IncrementAttemptStateRevision(attempt);
                }

                try
                {
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    return;
                }
                catch (DbUpdateConcurrencyException)
                {
                    _dbContext.ChangeTracker.Clear();
                    if (mutationAttempt == MaxAttemptMutationAttempts)
                        return;
                }
            }
        }

        private async Task<List<CourseTestDto>> BindTestsToAttemptVersionsAsync(
            IEnumerable<CourseTestDto> publishedTests,
            IEnumerable<TestAttempt> attempts,
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
                    // Never show tasks from a newer version as if they belonged to this attempt.
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

        private static Dictionary<string, string> FilterAnswers(
            TestAttempt attempt,
            Dictionary<string, string> answers)
        {
            var allowedTaskIds = DeserializeTaskIds(attempt.AllowedTaskIdsJson)
                .ToHashSet();

            return answers
                .Where(answer => allowedTaskIds.Contains(answer.Key))
                .ToDictionary(answer => answer.Key, answer => answer.Value);
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
                    CorrectOptionIds = new List<string>()
                }).ToList()
            };
        }

        private static LanguageModel ToLanguageModel(ReviewTeacherModelAccessResponse access)
        {
            return new LanguageModel
            {
                Key = access.ModelKey,
                DisplayName = access.DisplayName,
                Provider = "LLMGateway",
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

        private static bool ExpireAttemptIfNeeded(
            TestAttempt attempt,
            DateTimeOffset now)
        {
            if (attempt.Status != TestAttemptStatus.InProgress || attempt.EndsAt > now)
                return false;

            attempt.Status = TestAttemptStatus.Expired;
            return true;
        }

        private Task<TestAttempt?> FindStudentAttemptAsync(
            int attemptId,
            string studentUserId,
            CancellationToken cancellationToken)
        {
            return _dbContext.TestAttempts.SingleOrDefaultAsync(
                item => item.Id == attemptId
                    && item.StudentUserId.ToLower() == studentUserId.ToLower(),
                cancellationToken);
        }

        private static void IncrementAttemptStateRevision(TestAttempt attempt)
        {
            attempt.StateRevision = checked(attempt.StateRevision + 1);
        }

        private static TestAttemptResponse ToAttemptResponse(TestAttempt attempt)
        {
            return new TestAttemptResponse
            {
                Id = attempt.Id,
                TestId = attempt.TestId,
                TestRevision = attempt.TestRevision,
                Status = attempt.Status,
                StartedAt = attempt.StartedAt,
                EndsAt = attempt.EndsAt,
                SubmittedAt = attempt.SubmittedAt,
                Answers = DeserializeAnswers(attempt.AnswersJson)
            };
        }

        private static bool IsDuplicateAttempt(DbUpdateException exception)
        {
            return exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "IX_TestAttempts_TestId_StudentUserId"
            };
        }

        private static bool IsDuplicateAttemptSubmissionOutbox(DbUpdateException exception)
        {
            return exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "IX_AttemptSubmissionOutboxMessages_AttemptId"
            };
        }

        private static Dictionary<string, string> DeserializeAnswers(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new Dictionary<string, string>();

            return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions)
                ?? new Dictionary<string, string>();
        }

        private static string SerializeAnswers(Dictionary<string, string>? answers)
        {
            return JsonSerializer.Serialize(answers ?? new Dictionary<string, string>(), JsonOptions);
        }

        private static List<string> DeserializeTaskIds(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new List<string>();

            return JsonSerializer.Deserialize<List<string>>(json, JsonOptions)
                ?? new List<string>();
        }

        private static string SerializeTaskIds(IEnumerable<string> taskIds)
        {
            return JsonSerializer.Serialize(taskIds.Distinct().ToList(), JsonOptions);
        }

        private static DateTimeOffset Min(
            DateTimeOffset left,
            DateTimeOffset right)
        {
            return left <= right
                ? left
                : right;
        }
    }
}

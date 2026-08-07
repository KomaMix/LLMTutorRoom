using LLMTutorRoom.Data;
using LLMTutorRoom.DTOs;
using LLMTutorRoom.Models;
using LLMTutorRoom.Services.ReviewProcessing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace LLMTutorRoom.Services
{
    public sealed class ClassroomService
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly TutorRoomDbContext _dbContext;
        private readonly ReviewScoringService _reviewScoringService;
        private readonly IReviewQueuePublisher _reviewQueuePublisher;
        private readonly ReviewProcessingOptions _reviewProcessingOptions;
        private readonly ILogger<ClassroomService> _logger;

        public ClassroomService(
            TutorRoomDbContext dbContext,
            ReviewScoringService reviewScoringService,
            IReviewQueuePublisher reviewQueuePublisher,
            IOptions<ReviewProcessingOptions> reviewProcessingOptions,
            ILogger<ClassroomService> logger)
        {
            _dbContext = dbContext;
            _reviewScoringService = reviewScoringService;
            _reviewQueuePublisher = reviewQueuePublisher;
            _reviewProcessingOptions = reviewProcessingOptions.Value;
            _logger = logger;
        }

        public async Task<ClassroomOverview> GetTeacherOverviewAsync(CancellationToken cancellationToken)
        {
            var tests = await LoadTests()
                .OrderByDescending(test => test.Deadline)
                .ToListAsync(cancellationToken);
            var reviews = await LoadReviews()
                .OrderByDescending(review => review.SubmittedAt)
                .ToListAsync(cancellationToken);

            return new ClassroomOverview
            {
                Tests = tests,
                Models = Array.Empty<LanguageModel>(),
                Reviews = reviews,
                Attempts = Array.Empty<TestAttemptResponse>(),
                Metrics = CreateMetrics(tests, reviews)
            };
        }

        public async Task<ClassroomOverview> GetStudentOverviewAsync(
            string studentUserId,
            CancellationToken cancellationToken)
        {
            await ExpireStudentAttemptsAsync(studentUserId, cancellationToken);

            var tests = await LoadVisibleTests()
                .Where(test => test.Status == CourseTestStatus.Published)
                .OrderBy(test => test.Deadline)
                .ToListAsync(cancellationToken);
            var reviews = await LoadReviews()
                .Where(review => review.StudentUserId.ToLower() == studentUserId.ToLower())
                .OrderByDescending(review => review.SubmittedAt)
                .ToListAsync(cancellationToken);
            var attempts = await _dbContext.TestAttempts
                .AsNoTracking()
                .Where(attempt => attempt.StudentUserId.ToLower() == studentUserId.ToLower())
                .OrderByDescending(attempt => attempt.StartedAt)
                .ToListAsync(cancellationToken);

            return new ClassroomOverview
            {
                Tests = tests,
                Models = Array.Empty<LanguageModel>(),
                Reviews = reviews,
                Attempts = attempts.Select(ToAttemptResponse).ToList(),
                Metrics = CreateMetrics(tests, reviews)
            };
        }

        public async Task<IReadOnlyCollection<CourseTest>> GetTestsAsync(CancellationToken cancellationToken)
        {
            return await LoadTests()
                .OrderByDescending(test => test.Deadline)
                .ToListAsync(cancellationToken);
        }

        public async Task<CourseTest> CreateTestAsync(
            CreateTestRequest request,
            CancellationToken cancellationToken)
        {
            var test = new CourseTest
            {
                Id = CreateId("test"),
                Title = request.Title.Trim(),
                Subject = request.Subject.Trim(),
                Status = request.Status,
                Deadline = request.Deadline ?? DateTimeOffset.UtcNow.AddDays(7),
                TimeLimitMinutes = request.TimeLimitMinutes,
                Summary = request.Summary?.Trim() ?? string.Empty
            };

            _dbContext.Tests.Add(test);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return test;
        }

        public async Task<CourseTest?> UpdateTestAsync(
            string testId,
            CreateTestRequest request,
            CancellationToken cancellationToken)
        {
            var test = await _dbContext.Tests
                .SingleOrDefaultAsync(item => item.Id == testId, cancellationToken);

            if (test is null)
                return null;

            test.Title = request.Title.Trim();
            test.Subject = request.Subject.Trim();
            test.Status = request.Status;
            test.Deadline = request.Deadline ?? test.Deadline;
            test.TimeLimitMinutes = request.TimeLimitMinutes;
            test.Summary = request.Summary?.Trim() ?? string.Empty;

            await _dbContext.SaveChangesAsync(cancellationToken);
            return await LoadTests()
                .SingleAsync(item => item.Id == testId, cancellationToken);
        }

        public async Task<TestTask?> AddTaskAsync(
            string testId,
            CreateTaskRequest request,
            CancellationToken cancellationToken)
        {
            var testExists = await _dbContext.Tests
                .AnyAsync(test => test.Id == testId, cancellationToken);

            if (!testExists)
                return null;

            var taskId = CreateId("task");
            var task = new TestTask
            {
                Id = taskId,
                CourseTestId = testId,
                Type = request.Type,
                CheckMode = NormalizeTaskCheckMode(request),
                Title = request.Title.Trim(),
                Prompt = request.Prompt.Trim(),
                MaxPoints = request.MaxPoints,
                CreatedAt = DateTimeOffset.UtcNow,
                WrongAnswerPenalty = request.Type == TestTaskType.MultipleChoice
                    ? request.WrongAnswerPenalty
                    : 0,
                Options = request.Type == TestTaskType.FreeText
                    ? new List<AnswerOption>()
                    : CreateOptions(request, taskId)
            };

            _dbContext.TestTasks.Add(task);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return task;
        }

        public async Task<TestTask?> UpdateTaskAsync(
            string testId,
            string taskId,
            CreateTaskRequest request,
            CancellationToken cancellationToken)
        {
            var task = await _dbContext.TestTasks
                .Include(item => item.Options)
                .SingleOrDefaultAsync(
                    item => item.CourseTestId == testId && item.Id == taskId,
                    cancellationToken);

            if (task is null)
                return null;

            task.Type = request.Type;
            task.CheckMode = NormalizeTaskCheckMode(request);
            task.Title = request.Title.Trim();
            task.Prompt = request.Prompt.Trim();
            task.MaxPoints = request.MaxPoints;
            task.WrongAnswerPenalty = request.Type == TestTaskType.MultipleChoice
                ? request.WrongAnswerPenalty
                : 0;

            _dbContext.AnswerOptions.RemoveRange(task.Options);
            task.Options.Clear();

            if (request.Type != TestTaskType.FreeText)
            {
                foreach (var option in CreateOptions(request, task.Id))
                    task.Options.Add(option);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return task;
        }

        public async Task<TestTask?> SetTaskVisibilityAsync(
            string testId,
            string taskId,
            bool isHidden,
            CancellationToken cancellationToken)
        {
            var task = await _dbContext.TestTasks
                .SingleOrDefaultAsync(
                    item => item.CourseTestId == testId && item.Id == taskId,
                    cancellationToken);

            if (task is null)
                return null;

            task.IsHidden = isHidden;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return task;
        }

        public async Task<bool> DeleteTaskAsync(
            string testId,
            string taskId,
            CancellationToken cancellationToken)
        {
            var task = await _dbContext.TestTasks
                .SingleOrDefaultAsync(
                    item => item.CourseTestId == testId && item.Id == taskId,
                    cancellationToken);

            if (task is null)
                return false;

            _dbContext.TestTasks.Remove(task);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<TestAttemptResponse?> StartAttemptAsync(
            string testId,
            string studentUserId,
            CancellationToken cancellationToken)
        {
            await ExpireStudentAttemptsAsync(studentUserId, cancellationToken);

            var test = await LoadVisibleTests()
                .SingleOrDefaultAsync(
                    item => item.Id == testId && item.Status == CourseTestStatus.Published,
                    cancellationToken);

            if (test is null)
                return null;

            var existingAttempt = await _dbContext.TestAttempts
                .SingleOrDefaultAsync(
                    attempt => attempt.TestId == testId
                        && attempt.StudentUserId.ToLower() == studentUserId.ToLower(),
                    cancellationToken);

            if (existingAttempt is not null)
                return ToAttemptResponse(existingAttempt);

            var now = DateTimeOffset.UtcNow;
            if (test.Deadline <= now)
                return null;

            var attempt = new TestAttempt
            {
                TestId = test.Id,
                StudentUserId = studentUserId,
                Status = TestAttemptStatus.InProgress,
                StartedAt = now,
                EndsAt = Min(now.AddMinutes(test.TimeLimitMinutes), test.Deadline),
                AnswersJson = "{}"
            };

            _dbContext.TestAttempts.Add(attempt);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return ToAttemptResponse(attempt);
        }

        public async Task<TestAttemptResponse?> SaveAttemptAnswersAsync(
            int attemptId,
            string studentUserId,
            Dictionary<string, string> answers,
            CancellationToken cancellationToken)
        {
            var attempt = await _dbContext.TestAttempts
                .SingleOrDefaultAsync(
                    item => item.Id == attemptId
                        && item.StudentUserId.ToLower() == studentUserId.ToLower(),
                    cancellationToken);

            if (attempt is null)
                return null;

            var statusChanged = ExpireAttemptIfNeeded(attempt, DateTimeOffset.UtcNow);
            if (attempt.Status == TestAttemptStatus.InProgress)
            {
                attempt.AnswersJson = SerializeAnswers(await FilterAnswersAsync(
                    attempt.TestId,
                    answers,
                    cancellationToken));
            }

            if (statusChanged || attempt.Status == TestAttemptStatus.InProgress)
                await _dbContext.SaveChangesAsync(cancellationToken);

            return ToAttemptResponse(attempt);
        }

        public async Task<TestAttemptResponse?> SubmitAttemptAsync(
            int attemptId,
            string studentUserId,
            string studentDisplayName,
            CancellationToken cancellationToken)
        {
            var attempt = await _dbContext.TestAttempts
                .SingleOrDefaultAsync(
                    item => item.Id == attemptId
                        && item.StudentUserId.ToLower() == studentUserId.ToLower(),
                    cancellationToken);

            if (attempt is null)
                return null;

            var now = DateTimeOffset.UtcNow;
            var statusChanged = ExpireAttemptIfNeeded(attempt, now);
            if (attempt.Status == TestAttemptStatus.InProgress)
            {
                attempt.Status = TestAttemptStatus.Submitted;
                attempt.SubmittedAt = now;
                statusChanged = true;
            }

            if (statusChanged)
                await _dbContext.SaveChangesAsync(cancellationToken);

            if (attempt.Status == TestAttemptStatus.Submitted)
            {
                await EnsureReviewForAttemptAsync(
                    attempt,
                    studentDisplayName,
                    cancellationToken);
            }

            return ToAttemptResponse(attempt);
        }

        public async Task<SubmissionReview?> CreateReviewAsync(
            ReviewRequest request,
            string studentUserId,
            string studentName,
            CancellationToken cancellationToken)
        {
            var test = await LoadVisibleTests()
                .SingleOrDefaultAsync(item => item.Id == request.TestId, cancellationToken);

            if (test is null)
                return null;

            var review = CreateReviewEntity(
                test,
                attemptId: null,
                studentUserId,
                studentName,
                request.Answers);
            MoveDetachedLlmTasksToManualReview(review);

            _dbContext.SubmissionReviews.Add(review);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return review;
        }

        public async Task<SubmissionReview?> UpdateManualTaskReviewAsync(
            int reviewId,
            string taskId,
            ManualTaskReviewRequest request,
            CancellationToken cancellationToken)
        {
            var review = await _dbContext.SubmissionReviews
                .Include(item => item.TaskResults)
                .SingleOrDefaultAsync(item => item.Id == reviewId, cancellationToken);

            if (review is null)
                return null;

            var taskResult = review.TaskResults
                .SingleOrDefault(result => result.TaskId == taskId);

            if (taskResult is null || taskResult.Status != TaskReviewResultStatus.ManualReview)
                return null;

            taskResult.Score = Math.Clamp(
                Math.Round(request.Score, 1),
                0,
                taskResult.MaxScore);
            taskResult.Feedback = string.IsNullOrWhiteSpace(request.Feedback)
                ? "Проверено преподавателем."
                : request.Feedback.Trim();
            taskResult.Findings = request.Findings
                .Where(finding => !string.IsNullOrWhiteSpace(finding))
                .Select(finding => finding.Trim())
                .Take(5)
                .DefaultIfEmpty("Проверено преподавателем.")
                .ToList();
            taskResult.Status = TaskReviewResultStatus.Succeeded;
            taskResult.CompletedAt = DateTimeOffset.UtcNow;
            taskResult.NextRetryAt = null;
            taskResult.LastError = string.Empty;

            _reviewScoringService.RecalculateReview(review);
            review.Status = _reviewScoringService.GetReviewStatusAfterTaskProcessing(review);
            review.CompletedAt = review.Status == SubmissionReviewStatus.Checked
                ? DateTimeOffset.UtcNow
                : null;
            review.LastError = string.Empty;

            await _dbContext.SaveChangesAsync(cancellationToken);
            return review;
        }

        private IQueryable<CourseTest> LoadTests()
        {
            return _dbContext.Tests
                .AsNoTracking()
                .Include(test => test.Tasks
                    .OrderBy(task => task.CreatedAt)
                    .ThenBy(task => task.Id))
                .ThenInclude(task => task.Options);
        }

        private IQueryable<CourseTest> LoadVisibleTests()
        {
            return _dbContext.Tests
                .AsNoTracking()
                .Include(test => test.Tasks
                    .Where(task => !task.IsHidden)
                    .OrderBy(task => task.CreatedAt)
                    .ThenBy(task => task.Id))
                .ThenInclude(task => task.Options);
        }

        private IQueryable<SubmissionReview> LoadReviews()
        {
            return _dbContext.SubmissionReviews
                .AsNoTracking()
                .Include(review => review.TaskResults);
        }

        private async Task ExpireStudentAttemptsAsync(
            string studentUserId,
            CancellationToken cancellationToken)
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
                attempt.Status = TestAttemptStatus.Expired;

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        private async Task<Dictionary<string, string>> FilterAnswersAsync(
            string testId,
            Dictionary<string, string> answers,
            CancellationToken cancellationToken)
        {
            var taskIds = await _dbContext.TestTasks
                .AsNoTracking()
                .Where(task => task.CourseTestId == testId && !task.IsHidden)
                .Select(task => task.Id)
                .ToListAsync(cancellationToken);
            var allowedTaskIds = taskIds.ToHashSet();

            return answers
                .Where(answer => allowedTaskIds.Contains(answer.Key))
                .ToDictionary(answer => answer.Key, answer => answer.Value);
        }

        private static List<AnswerOption> CreateOptions(
            CreateTaskRequest request,
            string taskId)
        {
            var correctOptionIndexes = request.CorrectOptionIndexes
                .Distinct()
                .ToHashSet();

            return request.Options.Select((option, index) => new AnswerOption
            {
                Id = CreateId("option"),
                TestTaskId = taskId,
                Text = option.Trim(),
                IsCorrect = correctOptionIndexes.Contains(index)
            }).ToList();
        }

        private static DashboardMetrics CreateMetrics(
            IReadOnlyCollection<CourseTest> tests,
            IReadOnlyCollection<SubmissionReview> reviews)
        {
            var checkedReviews = reviews
                .Where(review => review.Status == SubmissionReviewStatus.Checked && review.MaxScore > 0)
                .ToList();

            return new DashboardMetrics
            {
                ActiveTests = tests.Count(test => test.Status == CourseTestStatus.Published),
                Tasks = tests.Sum(test => test.Tasks.Count(task => !task.IsHidden)),
                PendingReviews = reviews.Count(review =>
                    review.Status is SubmissionReviewStatus.Queued
                        or SubmissionReviewStatus.Processing
                        or SubmissionReviewStatus.RetryScheduled
                        or SubmissionReviewStatus.ManualReview),
                AverageScore = checkedReviews.Count == 0
                    ? 0
                    : Math.Round(checkedReviews.Average(review => review.Score / review.MaxScore * 100), 1)
            };
        }

        private async Task EnsureReviewForAttemptAsync(
            TestAttempt attempt,
            string studentDisplayName,
            CancellationToken cancellationToken)
        {
            var existingReview = await _dbContext.SubmissionReviews
                .SingleOrDefaultAsync(
                    review => review.AttemptId == attempt.Id,
                    cancellationToken);

            if (existingReview is not null)
            {
                if (existingReview.Status == SubmissionReviewStatus.Queued
                    && !existingReview.LastEnqueuedAt.HasValue)
                {
                    await TryPublishReviewAsync(existingReview, cancellationToken);
                }

                return;
            }

            var test = await LoadVisibleTests()
                .SingleOrDefaultAsync(item => item.Id == attempt.TestId, cancellationToken);

            if (test is null)
                return;

            var review = CreateReviewEntity(
                test,
                attempt.Id,
                attempt.StudentUserId,
                studentDisplayName,
                DeserializeAnswers(attempt.AnswersJson));

            _dbContext.SubmissionReviews.Add(review);
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (review.Status == SubmissionReviewStatus.Queued)
                await TryPublishReviewAsync(review, cancellationToken);
        }

        private SubmissionReview CreateReviewEntity(
            CourseTest test,
            int? attemptId,
            string studentUserId,
            string studentName,
            Dictionary<string, string> answers)
        {
            var taskResults = test.Tasks.Select(task =>
            {
                answers.TryGetValue(task.Id, out var answer);
                return _reviewScoringService.CreateInitialResult(task, answer ?? string.Empty);
            }).ToList();

            var totalScore = taskResults
                .Where(result => result.Status == TaskReviewResultStatus.Succeeded)
                .Sum(result => result.Score);
            var status = GetInitialReviewStatus(taskResults);
            var now = DateTimeOffset.UtcNow;

            return new SubmissionReview
            {
                AttemptId = attemptId,
                TestId = test.Id,
                TestTitle = test.Title,
                StudentUserId = studentUserId.Trim(),
                StudentName = string.IsNullOrWhiteSpace(studentName)
                    ? null
                    : studentName.Trim(),
                Status = status,
                ModelKey = _reviewProcessingOptions.LlmModelKey,
                SubmittedAt = now,
                QueuedAt = status == SubmissionReviewStatus.Queued ? now : null,
                CompletedAt = status == SubmissionReviewStatus.Checked ? now : null,
                Score = totalScore,
                MaxScore = test.TotalPoints,
                Summary = _reviewScoringService.CreateSummary(totalScore, test.TotalPoints),
                TaskResults = taskResults
            };
        }

        private async Task TryPublishReviewAsync(
            SubmissionReview review,
            CancellationToken cancellationToken)
        {
            if (!review.AttemptId.HasValue)
                return;

            var now = DateTimeOffset.UtcNow;
            try
            {
                await _reviewQueuePublisher.PublishAsync(
                    new ReviewQueueMessage
                    {
                        ReviewId = review.Id,
                        AttemptId = review.AttemptId.Value,
                        ModelKey = review.ModelKey,
                        RequestedAt = now
                    },
                    retryDelaySeconds: null,
                    cancellationToken);

                review.LastEnqueuedAt = now;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex,
                    "Could not publish review {ReviewId} to RabbitMQ. Maintenance worker will retry.",
                    review.Id);
            }
        }

        private static SubmissionReviewStatus GetInitialReviewStatus(
            IReadOnlyCollection<TaskReviewResult> taskResults)
        {
            if (taskResults.Any(result => result.Status == TaskReviewResultStatus.Pending))
                return SubmissionReviewStatus.Queued;

            if (taskResults.Any(result => result.Status == TaskReviewResultStatus.ManualReview))
                return SubmissionReviewStatus.ManualReview;

            if (taskResults.Any(result => result.Status == TaskReviewResultStatus.Failed))
                return SubmissionReviewStatus.Failed;

            return SubmissionReviewStatus.Checked;
        }

        private static void MoveDetachedLlmTasksToManualReview(SubmissionReview review)
        {
            if (review.AttemptId.HasValue)
                return;

            foreach (var result in review.TaskResults
                         .Where(result => result.Status == TaskReviewResultStatus.Pending))
            {
                result.Status = TaskReviewResultStatus.ManualReview;
                result.Feedback = "Ожидает ручной проверки.";
                result.Findings = new List<string>
                {
                    "Задание ожидает ручной проверки преподавателем."
                };
            }

            review.Status = GetInitialReviewStatus(review.TaskResults);
            review.QueuedAt = null;
        }

        private static TestTaskCheckMode NormalizeTaskCheckMode(CreateTaskRequest request)
        {
            if (request.Type != TestTaskType.FreeText)
                return TestTaskCheckMode.Auto;

            return request.CheckMode ?? TestTaskCheckMode.Llm;
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

        private static TestAttemptResponse ToAttemptResponse(TestAttempt attempt)
        {
            return new TestAttemptResponse
            {
                Id = attempt.Id,
                TestId = attempt.TestId,
                Status = attempt.Status,
                StartedAt = attempt.StartedAt,
                EndsAt = attempt.EndsAt,
                SubmittedAt = attempt.SubmittedAt,
                Answers = DeserializeAnswers(attempt.AnswersJson)
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

        private static DateTimeOffset Min(
            DateTimeOffset left,
            DateTimeOffset right)
        {
            return left <= right
                ? left
                : right;
        }

        private static string CreateId(string prefix)
        {
            return $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";
        }
    }
}

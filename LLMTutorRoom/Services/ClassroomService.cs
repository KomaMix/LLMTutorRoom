using System.Text.Json;
using LLMTutorRoom.Data;
using LLMTutorRoom.DTOs;
using LLMTutorRoom.Models;
using LLMTutorRoom.Services.ReviewProcessing;
using LLMTutorRoom.Services.Teaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TeachingService.Contracts.Enums;
using TeachingService.Contracts.Models;

namespace LLMTutorRoom.Services
{
    public sealed class ClassroomService
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly TutorRoomDbContext _dbContext;
        private readonly ITeachingServiceClient _teachingServiceClient;
        private readonly ReviewScoringService _reviewScoringService;
        private readonly IReviewQueuePublisher _reviewQueuePublisher;
        private readonly TeacherModelAccessService _modelAccessService;
        private readonly ReviewProcessingOptions _reviewProcessingOptions;
        private readonly ILogger<ClassroomService> _logger;

        public ClassroomService(
            TutorRoomDbContext dbContext,
            ITeachingServiceClient teachingServiceClient,
            ReviewScoringService reviewScoringService,
            IReviewQueuePublisher reviewQueuePublisher,
            TeacherModelAccessService modelAccessService,
            IOptions<ReviewProcessingOptions> reviewProcessingOptions,
            ILogger<ClassroomService> logger)
        {
            _dbContext = dbContext;
            _teachingServiceClient = teachingServiceClient;
            _reviewScoringService = reviewScoringService;
            _reviewQueuePublisher = reviewQueuePublisher;
            _modelAccessService = modelAccessService;
            _reviewProcessingOptions = reviewProcessingOptions.Value;
            _logger = logger;
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
            var reviews = await LoadReviews()
                .Where(review => review.TeacherUserId == teacherUserId)
                .OrderByDescending(review => review.SubmittedAt)
                .ToListAsync(cancellationToken);
            var modelAccess = await _modelAccessService.GetTeacherAccessAsync(
                teacherUserId,
                includeDisabled: false,
                cancellationToken);
            var models = modelAccess
                .Select(ToLanguageModel)
                .ToList();

            return new ClassroomOverview
            {
                Tests = tests
                    .OrderByDescending(test => test.Deadline)
                    .ToList(),
                Models = models,
                Reviews = reviews,
                Attempts = new List<TestAttemptResponse>(),
                Metrics = CreateMetrics(tests, reviews)
            };
        }

        public async Task<ClassroomOverview> GetStudentOverviewAsync(
            string studentUserId,
            CancellationToken cancellationToken)
        {
            await ExpireStudentAttemptsAsync(studentUserId, cancellationToken);

            var tests = await _teachingServiceClient.GetTestsAsync(
                publishedOnly: true,
                includeHidden: false,
                cancellationToken);
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
                Tests = tests
                    .OrderBy(test => test.Deadline)
                    .ToList(),
                Models = new List<LanguageModel>(),
                Reviews = reviews,
                Attempts = attempts.Select(ToAttemptResponse).ToList(),
                Metrics = CreateMetrics(tests, reviews)
            };
        }

        public async Task<TestAttemptResponse?> StartAttemptAsync(
            string testId,
            string studentUserId,
            CancellationToken cancellationToken)
        {
            await ExpireStudentAttemptsAsync(studentUserId, cancellationToken);

            var test = await _teachingServiceClient.GetTestAsync(
                testId,
                includeHidden: false,
                cancellationToken);

            if (test is null || test.Status != CourseTestStatus.Published)
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
            var test = await _teachingServiceClient.GetTestAsync(
                request.TestId,
                includeHidden: false,
                cancellationToken);

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
            var test = await _teachingServiceClient.GetTestAsync(
                testId,
                includeHidden: false,
                cancellationToken);

            if (test is null)
                return new Dictionary<string, string>();

            var allowedTaskIds = test.Tasks
                .Select(task => task.Id)
                .ToHashSet();

            return answers
                .Where(answer => allowedTaskIds.Contains(answer.Key))
                .ToDictionary(answer => answer.Key, answer => answer.Value);
        }

        private static DashboardMetrics CreateMetrics(
            List<CourseTestDto> tests,
            List<SubmissionReview> reviews)
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

            var test = await _teachingServiceClient.GetTestAsync(
                attempt.TestId,
                includeHidden: false,
                cancellationToken);

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
            CourseTestDto test,
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
                TeacherUserId = test.TeacherUserId.Trim(),
                StudentUserId = studentUserId.Trim(),
                StudentName = string.IsNullOrWhiteSpace(studentName)
                    ? null
                    : studentName.Trim(),
                Status = status,
                ModelKey = GetReviewModelKey(test),
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

            if (!await TryReserveLlmQuotaAsync(review, cancellationToken))
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

        private static SubmissionReviewStatus GetInitialReviewStatus(List<TaskReviewResult> taskResults)
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

        private async Task<bool> TryReserveLlmQuotaAsync(
            SubmissionReview review,
            CancellationToken cancellationToken)
        {
            if (!_reviewProcessingOptions.LlmGatewayEnabled)
                return true;

            if (review.LlmQuotaReservedAt.HasValue)
                return true;

            var llmCheckCount = review.TaskResults.Count(result =>
                result.CheckMode == TestTaskCheckMode.Llm
                && result.Status == TaskReviewResultStatus.Pending);

            if (llmCheckCount == 0)
                return true;

            if (string.IsNullOrWhiteSpace(review.TeacherUserId))
            {
                MovePendingLlmTasksToManualReview(
                    review,
                    "Для теста не указан преподаватель-владелец.");
                await _dbContext.SaveChangesAsync(cancellationToken);
                return false;
            }

            if (string.IsNullOrWhiteSpace(review.ModelKey))
            {
                MovePendingLlmTasksToManualReview(
                    review,
                    "Для теста не выбрана LLM-модель проверки.");
                await _dbContext.SaveChangesAsync(cancellationToken);
                return false;
            }

            var quota = await _modelAccessService.TryConsumeChecksAsync(
                review.TeacherUserId,
                review.ModelKey,
                llmCheckCount,
                cancellationToken);

            if (quota.Status == ModelQuotaConsumptionStatus.Allowed)
            {
                review.LlmQuotaReservedAt = DateTimeOffset.UtcNow;
                review.LlmQuotaReservationError = string.Empty;
                await _dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }

            MovePendingLlmTasksToManualReview(
                review,
                CreateQuotaReservationError(review.ModelKey, quota));
            await _dbContext.SaveChangesAsync(cancellationToken);
            return false;
        }

        private void MovePendingLlmTasksToManualReview(
            SubmissionReview review,
            string error)
        {
            foreach (var result in review.TaskResults
                         .Where(result => result.CheckMode == TestTaskCheckMode.Llm
                             && result.Status == TaskReviewResultStatus.Pending))
            {
                result.Status = TaskReviewResultStatus.ManualReview;
                result.NextRetryAt = null;
                result.LastError = error;
                result.Feedback = "Автоматическая проверка недоступна. Требуется ручная проверка.";
                result.Findings = new List<string>
                {
                    "Задание ожидает ручной проверки преподавателем."
                };
            }

            _reviewScoringService.RecalculateReview(review);
            review.Status = _reviewScoringService.GetReviewStatusAfterTaskProcessing(review);
            review.QueuedAt = null;
            review.NextRetryAt = null;
            review.ProcessingLeaseExpiresAt = null;
            review.LlmQuotaReservationError = error;
        }

        private static string CreateQuotaReservationError(
            string modelKey,
            ModelQuotaConsumptionResult quota)
        {
            return quota.Status switch
            {
                ModelQuotaConsumptionStatus.AccessNotFound =>
                    $"Преподавателю не выдан доступ к модели '{modelKey}'.",
                ModelQuotaConsumptionStatus.LimitExceeded =>
                    $"Лимит проверок для модели '{modelKey}' исчерпан. Осталось: {quota.RemainingChecks}.",
                ModelQuotaConsumptionStatus.InvalidCheckCount =>
                    "Количество LLM-проверок должно быть больше нуля.",
                ModelQuotaConsumptionStatus.GatewayUnavailable =>
                    "LLMGateway недоступен для резервирования лимита проверок.",
                _ => "Не удалось зарезервировать лимит LLM-проверок."
            };
        }

        private string GetReviewModelKey(CourseTestDto test)
        {
            if (!string.IsNullOrWhiteSpace(test.LlmModelKey))
                return test.LlmModelKey.Trim();

            return _reviewProcessingOptions.LlmModelKey;
        }

        private static LanguageModel ToLanguageModel(TeacherModelAccessResponse access)
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
    }
}

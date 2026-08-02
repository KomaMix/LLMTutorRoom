using LLMTutorRoom.Data;
using LLMTutorRoom.DTOs;
using LLMTutorRoom.Models;
using Microsoft.EntityFrameworkCore;

namespace LLMTutorRoom.Services
{
    public sealed class ClassroomService
    {
        private readonly TutorRoomDbContext _dbContext;

        public ClassroomService(TutorRoomDbContext dbContext)
        {
            _dbContext = dbContext;
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
                Metrics = CreateMetrics(tests, reviews)
            };
        }

        public async Task<ClassroomOverview> GetStudentOverviewAsync(
            string studentName,
            CancellationToken cancellationToken)
        {
            var tests = await LoadVisibleTests()
                .Where(test => test.Status == CourseTestStatus.Published)
                .OrderBy(test => test.Deadline)
                .ToListAsync(cancellationToken);
            var reviews = await LoadReviews()
                .Where(review => review.StudentName.ToLower() == studentName.ToLower())
                .OrderByDescending(review => review.SubmittedAt)
                .ToListAsync(cancellationToken);

            return new ClassroomOverview
            {
                Tests = tests,
                Models = Array.Empty<LanguageModel>(),
                Reviews = reviews,
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

        public async Task<SubmissionReview?> CreateReviewAsync(
            ReviewRequest request,
            string studentName,
            CancellationToken cancellationToken)
        {
            var test = await LoadVisibleTests()
                .SingleOrDefaultAsync(item => item.Id == request.TestId, cancellationToken);

            if (test is null)
                return null;

            var taskResults = test.Tasks.Select(task =>
            {
                request.Answers.TryGetValue(task.Id, out var answer);
                return CreateTaskReview(task, answer ?? string.Empty);
            }).ToList();

            var totalScore = taskResults.Sum(result => result.Score);
            var review = new SubmissionReview
            {
                TestId = test.Id,
                TestTitle = test.Title,
                StudentName = string.IsNullOrWhiteSpace(studentName)
                    ? "Студент"
                    : studentName.Trim(),
                Status = SubmissionReviewStatus.Checked,
                ModelKey = string.Empty,
                SubmittedAt = DateTimeOffset.UtcNow,
                Score = totalScore,
                MaxScore = test.TotalPoints,
                Summary = CreateSummary(totalScore, test.TotalPoints),
                TaskResults = taskResults
            };

            _dbContext.SubmissionReviews.Add(review);
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
                PendingReviews = reviews.Count(review => review.Status == SubmissionReviewStatus.Queued),
                AverageScore = checkedReviews.Count == 0
                    ? 0
                    : Math.Round(checkedReviews.Average(review => review.Score / review.MaxScore * 100), 1)
            };
        }

        private static TaskReviewResult CreateTaskReview(TestTask task, string answer)
        {
            if (task.Type == TestTaskType.SingleChoice)
                return CreateSingleChoiceTaskReview(task, answer);

            if (task.Type == TestTaskType.MultipleChoice)
                return CreateMultipleChoiceTaskReview(task, answer);

            return CreateFreeTextTaskReview(task, answer);
        }

        private static TaskReviewResult CreateSingleChoiceTaskReview(TestTask task, string answer)
        {
            var selectedOptionIds = answer
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet();
            var correctOptionIds = task.CorrectOptionIds.ToHashSet();
            var isCorrect = selectedOptionIds.SetEquals(correctOptionIds);

            return new TaskReviewResult
            {
                TaskId = task.Id,
                TaskTitle = task.Title,
                Score = isCorrect ? task.MaxPoints : 0,
                MaxScore = task.MaxPoints,
                Feedback = isCorrect
                    ? "Ответ выбран верно."
                    : "Ответ не совпадает с правильным вариантом.",
                Findings = isCorrect
                    ? new List<string> { "Выбран корректный вариант." }
                    : new List<string> { "Нужно повторить материал по этому вопросу." }
            };
        }

        private static TaskReviewResult CreateMultipleChoiceTaskReview(TestTask task, string answer)
        {
            var selectedOptionIds = answer
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet();
            var correctOptionIds = task.CorrectOptionIds.ToHashSet();
            var selectedCorrectCount = selectedOptionIds.Count(correctOptionIds.Contains);
            var selectedWrongCount = selectedOptionIds.Count(optionId => !correctOptionIds.Contains(optionId));
            var pointsPerCorrectOption = correctOptionIds.Count == 0
                ? 0
                : task.MaxPoints / correctOptionIds.Count;
            var score = selectedCorrectCount * pointsPerCorrectOption
                - selectedWrongCount * task.WrongAnswerPenalty;

            score = Math.Clamp(Math.Round(score, 1), 0, task.MaxPoints);

            return new TaskReviewResult
            {
                TaskId = task.Id,
                TaskTitle = task.Title,
                Score = score,
                MaxScore = task.MaxPoints,
                Feedback = selectedWrongCount == 0 && selectedCorrectCount == correctOptionIds.Count
                    ? "Все правильные варианты выбраны."
                    : "Баллы начислены за правильные варианты с учетом штрафа за неверные.",
                Findings = new List<string>
                {
                    $"Правильных вариантов выбрано: {selectedCorrectCount}.",
                    $"Неверных вариантов выбрано: {selectedWrongCount}."
                }
            };
        }

        private static TaskReviewResult CreateFreeTextTaskReview(TestTask task, string answer)
        {
            var normalizedAnswer = answer.Trim();
            var score = 0m;
            var findings = new List<string>();

            if (normalizedAnswer.Length > 120)
            {
                score += task.MaxPoints * 0.6m;
                findings.Add("Ответ содержит развернутое объяснение.");
            }
            else if (normalizedAnswer.Length > 40)
            {
                score += task.MaxPoints * 0.35m;
                findings.Add("Ответ содержит базовую аргументацию.");
            }
            else
            {
                findings.Add("Ответ слишком короткий для уверенной проверки.");
            }

            score = Math.Min(task.MaxPoints, Math.Round(score, 1));

            return new TaskReviewResult
            {
                TaskId = task.Id,
                TaskTitle = task.Title,
                Score = score,
                MaxScore = task.MaxPoints,
                Feedback = score >= task.MaxPoints * 0.75m
                    ? "Ответ выглядит достаточно полным."
                    : "Ответ требует доработки: добавь ход рассуждения и обоснование вывода.",
                Findings = findings
            };
        }

        private static string CreateSummary(decimal score, decimal maxScore)
        {
            var percent = maxScore == 0 ? 0 : score / maxScore;
            if (percent >= 0.8m)
                return "Работа в целом соответствует требованиям.";

            if (percent >= 0.55m)
                return "Работа частично соответствует требованиям.";

            return "Работа пока не закрывает ключевые требования.";
        }

        private static string CreateId(string prefix)
        {
            return $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";
        }
    }
}

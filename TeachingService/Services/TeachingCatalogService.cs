using Microsoft.EntityFrameworkCore;
using TeachingService.Contracts.Enums;
using TeachingService.Contracts.Models;
using TeachingService.Contracts.Requests;
using TeachingService.Data;
using TeachingService.Mappers;
using TeachingService.Models;

namespace TeachingService.Services
{
    public sealed class TeachingCatalogService
    {
        private readonly TeachingDbContext _dbContext;

        public TeachingCatalogService(TeachingDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<CourseTestDto>> GetTestsAsync(
            bool publishedOnly,
            bool includeHidden,
            CancellationToken cancellationToken)
        {
            var query = LoadTests(includeHidden);
            if (publishedOnly)
                query = query.Where(test => test.Status == CourseTestStatus.Published);

            var tests = await query
                .OrderByDescending(test => test.Deadline)
                .ToListAsync(cancellationToken);

            return tests
                .Select(test => CourseTestMapper.ToDto(test, includeHidden))
                .ToList();
        }

        public async Task<CourseTestDto?> GetTestAsync(
            Guid testId,
            bool includeHidden,
            CancellationToken cancellationToken)
        {
            var test = await LoadTests(includeHidden)
                .SingleOrDefaultAsync(item => item.Id == testId, cancellationToken);

            return test is null
                ? null
                : CourseTestMapper.ToDto(test, includeHidden);
        }

        public async Task<CourseTestDto> CreateTestAsync(
            CreateTestRequest request,
            CancellationToken cancellationToken)
        {
            var test = new CourseTest
            {
                Title = request.Title.Trim(),
                Subject = request.Subject.Trim(),
                Status = request.Status,
                Deadline = GetRequiredDeadline(request),
                TimeLimitMinutes = request.TimeLimitMinutes,
                Summary = request.Summary?.Trim() ?? string.Empty
            };

            _dbContext.Tests.Add(test);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return CourseTestMapper.ToDto(test, includeHidden: true);
        }

        public async Task<CourseTestDto?> UpdateTestAsync(
            Guid testId,
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
            test.Deadline = GetRequiredDeadline(request);
            test.TimeLimitMinutes = request.TimeLimitMinutes;
            test.Summary = request.Summary?.Trim() ?? string.Empty;

            await _dbContext.SaveChangesAsync(cancellationToken);
            return await GetTestAsync(testId, includeHidden: true, cancellationToken);
        }

        public async Task<TestTaskDto?> AddTaskAsync(
            Guid testId,
            CreateTaskRequest request,
            CancellationToken cancellationToken)
        {
            var testExists = await _dbContext.Tests
                .AnyAsync(test => test.Id == testId, cancellationToken);

            if (!testExists)
                return null;

            var task = new TestTask
            {
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
                    : CreateOptions(request)
            };

            _dbContext.TestTasks.Add(task);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return CourseTestMapper.ToDto(task);
        }

        public async Task<TestTaskDto?> UpdateTaskAsync(
            Guid testId,
            Guid taskId,
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
                foreach (var option in CreateOptions(request))
                    task.Options.Add(option);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return CourseTestMapper.ToDto(task);
        }

        public async Task<TestTaskDto?> SetTaskVisibilityAsync(
            Guid testId,
            Guid taskId,
            bool isHidden,
            CancellationToken cancellationToken)
        {
            var task = await _dbContext.TestTasks
                .Include(item => item.Options)
                .SingleOrDefaultAsync(
                    item => item.CourseTestId == testId && item.Id == taskId,
                    cancellationToken);

            if (task is null)
                return null;

            task.IsHidden = isHidden;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return CourseTestMapper.ToDto(task);
        }

        public async Task<bool> DeleteTaskAsync(
            Guid testId,
            Guid taskId,
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

        private IQueryable<CourseTest> LoadTests(bool includeHidden)
        {
            if (includeHidden)
            {
                return _dbContext.Tests
                    .AsNoTracking()
                    .Include(test => test.Tasks
                        .OrderBy(task => task.CreatedAt)
                        .ThenBy(task => task.Id))
                    .ThenInclude(task => task.Options);
            }

            return _dbContext.Tests
                .AsNoTracking()
                .Include(test => test.Tasks
                    .Where(task => !task.IsHidden)
                    .OrderBy(task => task.CreatedAt)
                    .ThenBy(task => task.Id))
                .ThenInclude(task => task.Options);
        }

        private static List<AnswerOption> CreateOptions(CreateTaskRequest request)
        {
            var correctOptionIndexes = request.CorrectOptionIndexes
                .Distinct()
                .ToHashSet();

            return request.Options.Select((option, index) => new AnswerOption
            {
                Text = option.Trim(),
                IsCorrect = correctOptionIndexes.Contains(index)
            }).ToList();
        }

        private static TestTaskCheckMode NormalizeTaskCheckMode(CreateTaskRequest request)
        {
            if (request.Type != TestTaskType.FreeText)
                return TestTaskCheckMode.Auto;

            return request.CheckMode ?? TestTaskCheckMode.Llm;
        }

        private static DateTimeOffset GetRequiredDeadline(CreateTestRequest request)
        {
            return request.Deadline
                ?? throw new ArgumentException("Deadline is required.", nameof(request));
        }
    }
}

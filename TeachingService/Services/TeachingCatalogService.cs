using Microsoft.EntityFrameworkCore;
using TeachingService.Contracts.Enums;
using TeachingService.Contracts.Models;
using TeachingService.Contracts.Requests;
using TeachingService.Data;
using TeachingService.Enums;
using TeachingService.Interfaces;
using TeachingService.Mappers;
using TeachingService.Models;

namespace TeachingService.Services
{
    public sealed class TeachingCatalogService
    {
        private readonly TeachingDbContext _dbContext;
        private readonly IReviewPolicyOutboxWriter _reviewPolicyOutboxWriter;

        public TeachingCatalogService(
            TeachingDbContext dbContext,
            IReviewPolicyOutboxWriter reviewPolicyOutboxWriter)
        {
            _dbContext = dbContext;
            _reviewPolicyOutboxWriter = reviewPolicyOutboxWriter;
        }

        public async Task<List<CourseTestDto>> GetAllTestsAsync(
            bool publishedOnly,
            bool includeHidden,
            CancellationToken cancellationToken)
        {
            var tests = await LoadTestCatalog()
                .Where(test => test.TeacherUserId != string.Empty)
                .ToListAsync(cancellationToken);

            return await MapSelectedVersionsAsync(
                tests,
                test => publishedOnly
                    ? GetPublishedVersion(test)
                    : GetDraftVersion(test) ?? GetPublishedVersion(test),
                includeHidden,
                cancellationToken);
        }

        public async Task<List<CourseTestDto>> GetTeacherTestsAsync(
            string teacherUserId,
            bool includeHidden,
            CancellationToken cancellationToken)
        {
            EnsureTeacherUserId(teacherUserId);

            var tests = await LoadTestCatalog()
                .Where(test => test.TeacherUserId == teacherUserId)
                .ToListAsync(cancellationToken);

            return await MapSelectedVersionsAsync(
                tests,
                test => GetDraftVersion(test) ?? GetPublishedVersion(test),
                includeHidden,
                cancellationToken);
        }

        public Task<CourseTestDto?> GetTestAsync(
            Guid testId,
            int? versionNumber,
            bool includeHidden,
            CancellationToken cancellationToken)
        {
            return GetTestVersionAsync(
                testId,
                teacherUserId: null,
                versionNumber,
                includeDraft: false,
                includeHidden,
                cancellationToken);
        }

        public Task<CourseTestDto?> GetTeacherTestAsync(
            Guid testId,
            string teacherUserId,
            bool includeHidden,
            CancellationToken cancellationToken)
        {
            EnsureTeacherUserId(teacherUserId);
            return GetTestVersionAsync(
                testId,
                teacherUserId,
                versionNumber: null,
                includeDraft: true,
                includeHidden,
                cancellationToken);
        }

        public Task<CourseTestDto?> GetTeacherTestVersionAsync(
            Guid testId,
            int versionNumber,
            string teacherUserId,
            bool includeHidden,
            CancellationToken cancellationToken)
        {
            EnsureTeacherUserId(teacherUserId);
            return GetTestVersionAsync(
                testId,
                teacherUserId,
                versionNumber,
                includeDraft: true,
                includeHidden,
                cancellationToken);
        }

        public async Task<List<CourseTestVersionSummaryDto>?> GetTeacherTestVersionsAsync(
            Guid testId,
            string teacherUserId,
            CancellationToken cancellationToken)
        {
            EnsureTeacherUserId(teacherUserId);

            var test = await LoadTestCatalog()
                .SingleOrDefaultAsync(
                    item => item.Id == testId && item.TeacherUserId == teacherUserId,
                    cancellationToken);

            if (test is null)
                return null;

            var versions = test.Versions
                .OrderByDescending(version => version.VersionNumber)
                .Select(CourseTestMapper.ToSummaryDto)
                .ToList();
            return versions;
        }

        public async Task<CourseTestDto> CreateTestAsync(
            CreateTestRequest request,
            string teacherUserId,
            CancellationToken cancellationToken)
        {
            EnsureTeacherUserId(teacherUserId);

            var testId = Guid.NewGuid();
            var version = CreateVersion(
                testId,
                versionNumber: 1,
                request,
                DateTimeOffset.UtcNow);
            var test = new CourseTest
            {
                Id = testId,
                TeacherUserId = teacherUserId,
                NextVersionNumber = 2,
                Versions = [version]
            };
            version.CourseTest = test;

            _dbContext.Tests.Add(test);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return CourseTestMapper.ToDto(test, version, includeHidden: true);
        }

        public async Task<Result<CatalogOperationStatus, CourseTestDto>> CreateDraftVersionAsync(
            Guid testId,
            int expectedPublishedVersionNumber,
            string teacherUserId,
            CancellationToken cancellationToken)
        {
            EnsureTeacherUserId(teacherUserId);

            var test = await GetTeacherOwnedActiveAggregateAsync(
                testId,
                teacherUserId,
                cancellationToken);
            if (test is null)
                return new(CatalogOperationStatus.NotFound);

            if (GetDraftVersion(test) is not null)
            {
                return new(
                    CatalogOperationStatus.Conflict,
                    error: "The test already has an editable draft.");
            }

            var publishedVersion = GetPublishedVersion(test);
            if (publishedVersion is null)
            {
                return new(
                    CatalogOperationStatus.Conflict,
                    error: "A new version can only be created from a published version.");
            }
            if (publishedVersion.VersionNumber != expectedPublishedVersionNumber)
            {
                return new(
                    CatalogOperationStatus.Conflict,
                    error: "The published source version changed. Reload the test before creating a draft.");
            }

            var versionNumber = test.NextVersionNumber;
            var draft = CloneAsDraft(publishedVersion, versionNumber, DateTimeOffset.UtcNow);
            test.NextVersionNumber = checked(versionNumber + 1);
            test.Versions.Add(draft);
            _dbContext.TestVersions.Add(draft);

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return new(
                    CatalogOperationStatus.Conflict,
                    error: "The test was changed concurrently. Reload it and try again.");
            }
            catch (DbUpdateException)
            {
                return new(
                    CatalogOperationStatus.Conflict,
                    error: "A draft was created concurrently. Reload the test.");
            }

            var dto = await GetTeacherTestVersionAsync(
                testId,
                versionNumber,
                teacherUserId,
                includeHidden: true,
                cancellationToken);
            return new(CatalogOperationStatus.Success, dto!);
        }

        public async Task<Result<CatalogOperationStatus, CourseTestDto>> UpdateTestAsync(
            Guid testId,
            int expectedVersionNumber,
            int expectedContentRevision,
            CreateTestRequest request,
            string teacherUserId,
            CancellationToken cancellationToken)
        {
            EnsureTeacherUserId(teacherUserId);

            var test = await GetTeacherOwnedActiveAggregateAsync(
                testId,
                teacherUserId,
                cancellationToken);
            if (test is null)
                return new(CatalogOperationStatus.NotFound);

            var draft = GetDraftVersion(test);
            if (draft is null)
            {
                return new(
                    CatalogOperationStatus.Conflict,
                    error: "Published versions are immutable. Create a new draft version first.");
            }
            if (draft.VersionNumber != expectedVersionNumber)
            {
                return new(
                    CatalogOperationStatus.Conflict,
                    error: "The requested draft version is no longer current. Reload the test.");
            }
            if (draft.ContentRevision != expectedContentRevision)
            {
                return new(
                    CatalogOperationStatus.Conflict,
                    error: "The draft content changed. Reload it before saving.");
            }

            ApplyTestDetails(draft, request);
            if (!await TrySaveDraftMutationAsync(draft, cancellationToken))
            {
                return new(
                    CatalogOperationStatus.Conflict,
                    error: "The draft changed concurrently or was published. Reload it and try again.");
            }

            var dto = await GetTeacherTestVersionAsync(
                testId,
                draft.VersionNumber,
                teacherUserId,
                includeHidden: true,
                cancellationToken);
            return new(CatalogOperationStatus.Success, dto!);
        }

        public async Task<Result<CatalogOperationStatus, CourseTestDto>> PublishVersionAsync(
            Guid testId,
            int versionNumber,
            int expectedContentRevision,
            string teacherUserId,
            CancellationToken cancellationToken)
        {
            EnsureTeacherUserId(teacherUserId);

            var test = await GetTeacherOwnedVersionCatalogTrackingAsync(
                testId,
                teacherUserId,
                cancellationToken);
            if (test is null)
                return new(CatalogOperationStatus.NotFound);

            var version = test.Versions.SingleOrDefault(
                item => item.VersionNumber == versionNumber);
            if (version is null)
                return new(CatalogOperationStatus.NotFound);

            if (version.Status != CourseTestStatus.Draft)
            {
                return new(
                    CatalogOperationStatus.Conflict,
                    error: "Only a draft version can be published.");
            }
            if (version.ContentRevision != expectedContentRevision)
            {
                return new(
                    CatalogOperationStatus.Conflict,
                    error: "The draft content changed. Reload it before publishing.");
            }

            await _dbContext.Entry(version)
                .Collection(item => item.Tasks)
                .Query()
                .Include(task => task.Options)
                .LoadAsync(cancellationToken);

            var validationError = ValidateForPublishing(version, DateTimeOffset.UtcNow);
            if (validationError is not null)
                return new(CatalogOperationStatus.ValidationFailed, error: validationError);

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(
                cancellationToken);
            try
            {
                // The active-status unique index permits only one Published version. Persist the
                // old status first, but keep both saves in one transaction so consumers can
                // never observe a test without its new policy event.
                var previouslyPublished = GetPublishedVersion(test);
                if (previouslyPublished is not null)
                {
                    previouslyPublished.Status = CourseTestStatus.Superseded;
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }

                var publishedAt = DateTimeOffset.UtcNow;
                version.ContentRevision = checked(version.ContentRevision + 1);
                version.Status = CourseTestStatus.Published;
                version.PublishedAt = publishedAt;
                _reviewPolicyOutboxWriter.StagePublishedRevision(test, version);

                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                return new(
                    CatalogOperationStatus.Conflict,
                    error: "The version was published concurrently. Reload the test.");
            }

            var dto = await GetTeacherTestVersionAsync(
                testId,
                versionNumber,
                teacherUserId,
                includeHidden: true,
                cancellationToken);
            return new(CatalogOperationStatus.Success, dto!);
        }

        public async Task<Result<CatalogOperationStatus, bool>> DeleteDraftVersionAsync(
            Guid testId,
            int versionNumber,
            int expectedContentRevision,
            string teacherUserId,
            CancellationToken cancellationToken)
        {
            EnsureTeacherUserId(teacherUserId);

            var test = await GetTeacherOwnedVersionCatalogTrackingAsync(
                testId,
                teacherUserId,
                cancellationToken);
            if (test is null)
                return new(CatalogOperationStatus.NotFound);

            var version = test.Versions.SingleOrDefault(
                item => item.VersionNumber == versionNumber);
            if (version is null)
                return new(CatalogOperationStatus.NotFound);

            if (version.Status != CourseTestStatus.Draft)
            {
                return new(
                    CatalogOperationStatus.Conflict,
                    error: "Published and superseded versions cannot be deleted.");
            }
            if (version.ContentRevision != expectedContentRevision)
            {
                return new(
                    CatalogOperationStatus.Conflict,
                    error: "The draft content changed. Reload it before deleting.");
            }

            var hasOtherVersions = await _dbContext.TestVersions.AnyAsync(
                item => item.CourseTestId == testId && item.Id != version.Id,
                cancellationToken);

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(
                cancellationToken);
            try
            {
                _dbContext.TestVersions.Remove(version);
                await _dbContext.SaveChangesAsync(cancellationToken);

                if (!hasOtherVersions)
                {
                    _dbContext.Tests.Remove(test);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return new(
                    CatalogOperationStatus.Conflict,
                    error: "The draft changed concurrently or was published. Reload the test.");
            }

            return new(CatalogOperationStatus.Success, true);
        }

        public async Task<Result<CatalogOperationStatus, TestTaskDto>> AddTaskAsync(
            Guid testId,
            int expectedVersionNumber,
            int expectedContentRevision,
            CreateTaskRequest request,
            string teacherUserId,
            CancellationToken cancellationToken)
        {
            EnsureTeacherUserId(teacherUserId);

            var aggregate = await GetEditableDraftAsync(testId, teacherUserId, cancellationToken);
            if (aggregate.Test is null)
                return new(CatalogOperationStatus.NotFound);
            if (aggregate.Draft is null)
            {
                return new(
                    CatalogOperationStatus.Conflict,
                    error: "Published versions are immutable. Create a new draft version first.");
            }
            if (aggregate.Draft.VersionNumber != expectedVersionNumber)
            {
                return new(
                    CatalogOperationStatus.Conflict,
                    error: "The requested draft version is no longer current. Reload the test.");
            }
            if (aggregate.Draft.ContentRevision != expectedContentRevision)
            {
                return new(
                    CatalogOperationStatus.Conflict,
                    error: "The draft content changed. Reload it before saving.");
            }

            var task = CreateTask(aggregate.Draft.Id, request);
            aggregate.Draft.Tasks.Add(task);
            _dbContext.TestTasks.Add(task);
            if (!await TrySaveDraftMutationAsync(aggregate.Draft, cancellationToken))
            {
                return new(
                    CatalogOperationStatus.Conflict,
                    error: "The draft changed concurrently or was published. Reload it and try again.");
            }

            return new(CatalogOperationStatus.Success, CourseTestMapper.ToDto(task));
        }

        public async Task<Result<CatalogOperationStatus, TestTaskDto>> UpdateTaskAsync(
            Guid testId,
            int expectedVersionNumber,
            int expectedContentRevision,
            Guid taskId,
            CreateTaskRequest request,
            string teacherUserId,
            CancellationToken cancellationToken)
        {
            EnsureTeacherUserId(teacherUserId);

            var aggregate = await GetEditableDraftAsync(testId, teacherUserId, cancellationToken);
            if (aggregate.Test is null)
                return new(CatalogOperationStatus.NotFound);
            if (aggregate.Draft is null)
            {
                return new(
                    CatalogOperationStatus.Conflict,
                    error: "Published versions are immutable. Create a new draft version first.");
            }
            if (aggregate.Draft.VersionNumber != expectedVersionNumber)
            {
                return new(
                    CatalogOperationStatus.Conflict,
                    error: "The requested draft version is no longer current. Reload the test.");
            }
            if (aggregate.Draft.ContentRevision != expectedContentRevision)
            {
                return new(
                    CatalogOperationStatus.Conflict,
                    error: "The draft content changed. Reload it before saving.");
            }

            var task = aggregate.Draft.Tasks.SingleOrDefault(item => item.Id == taskId);
            if (task is null)
                return new(CatalogOperationStatus.NotFound);

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
                var replacementOptions = CreateOptions(task.Id, request);
                foreach (var option in replacementOptions)
                    task.Options.Add(option);
                _dbContext.AnswerOptions.AddRange(replacementOptions);
            }

            if (!await TrySaveDraftMutationAsync(aggregate.Draft, cancellationToken))
            {
                return new(
                    CatalogOperationStatus.Conflict,
                    error: "The draft changed concurrently or was published. Reload it and try again.");
            }
            return new(CatalogOperationStatus.Success, CourseTestMapper.ToDto(task));
        }

        public async Task<Result<CatalogOperationStatus, TestTaskDto>> SetTaskVisibilityAsync(
            Guid testId,
            int expectedVersionNumber,
            int expectedContentRevision,
            Guid taskId,
            bool isHidden,
            string teacherUserId,
            CancellationToken cancellationToken)
        {
            EnsureTeacherUserId(teacherUserId);

            var aggregate = await GetEditableDraftAsync(testId, teacherUserId, cancellationToken);
            if (aggregate.Test is null)
                return new(CatalogOperationStatus.NotFound);
            if (aggregate.Draft is null)
            {
                return new(
                    CatalogOperationStatus.Conflict,
                    error: "Published versions are immutable. Create a new draft version first.");
            }
            if (aggregate.Draft.VersionNumber != expectedVersionNumber)
            {
                return new(
                    CatalogOperationStatus.Conflict,
                    error: "The requested draft version is no longer current. Reload the test.");
            }
            if (aggregate.Draft.ContentRevision != expectedContentRevision)
            {
                return new(
                    CatalogOperationStatus.Conflict,
                    error: "The draft content changed. Reload it before saving.");
            }

            var task = aggregate.Draft.Tasks.SingleOrDefault(item => item.Id == taskId);
            if (task is null)
                return new(CatalogOperationStatus.NotFound);

            task.IsHidden = isHidden;
            if (!await TrySaveDraftMutationAsync(aggregate.Draft, cancellationToken))
            {
                return new(
                    CatalogOperationStatus.Conflict,
                    error: "The draft changed concurrently or was published. Reload it and try again.");
            }
            return new(CatalogOperationStatus.Success, CourseTestMapper.ToDto(task));
        }

        public async Task<Result<CatalogOperationStatus, bool>> DeleteTaskAsync(
            Guid testId,
            int expectedVersionNumber,
            int expectedContentRevision,
            Guid taskId,
            string teacherUserId,
            CancellationToken cancellationToken)
        {
            EnsureTeacherUserId(teacherUserId);

            var aggregate = await GetEditableDraftAsync(testId, teacherUserId, cancellationToken);
            if (aggregate.Test is null)
                return new(CatalogOperationStatus.NotFound);
            if (aggregate.Draft is null)
            {
                return new(
                    CatalogOperationStatus.Conflict,
                    error: "Published versions are immutable. Create a new draft version first.");
            }
            if (aggregate.Draft.VersionNumber != expectedVersionNumber)
            {
                return new(
                    CatalogOperationStatus.Conflict,
                    error: "The requested draft version is no longer current. Reload the test.");
            }
            if (aggregate.Draft.ContentRevision != expectedContentRevision)
            {
                return new(
                    CatalogOperationStatus.Conflict,
                    error: "The draft content changed. Reload it before saving.");
            }

            var task = aggregate.Draft.Tasks.SingleOrDefault(item => item.Id == taskId);
            if (task is null)
                return new(CatalogOperationStatus.NotFound);

            _dbContext.TestTasks.Remove(task);
            if (!await TrySaveDraftMutationAsync(aggregate.Draft, cancellationToken))
            {
                return new(
                    CatalogOperationStatus.Conflict,
                    error: "The draft changed concurrently or was published. Reload it and try again.");
            }
            return new(CatalogOperationStatus.Success, true);
        }

        private async Task<CourseTestDto?> GetTestVersionAsync(
            Guid testId,
            string? teacherUserId,
            int? versionNumber,
            bool includeDraft,
            bool includeHidden,
            CancellationToken cancellationToken)
        {
            var query = LoadTestCatalog().Where(test => test.Id == testId);
            if (teacherUserId is not null)
                query = query.Where(test => test.TeacherUserId == teacherUserId);

            var test = await query.SingleOrDefaultAsync(cancellationToken);
            if (test is null)
                return null;

            CourseTestVersion? version;
            if (versionNumber.HasValue)
            {
                version = test.Versions.SingleOrDefault(
                    item => item.VersionNumber == versionNumber.Value
                        && (includeDraft || item.Status != CourseTestStatus.Draft));
            }
            else
            {
                version = includeDraft
                    ? GetDraftVersion(test) ?? GetPublishedVersion(test)
                    : GetPublishedVersion(test);
            }

            if (version is null)
                return null;

            var details = await LoadVersionDetails(includeHidden)
                .SingleOrDefaultAsync(item => item.Id == version.Id, cancellationToken);
            if (details is null)
                return null;

            return CourseTestMapper.ToDto(test, details, includeHidden);
        }

        private async Task<List<CourseTestDto>> MapSelectedVersionsAsync(
            List<CourseTest> tests,
            Func<CourseTest, CourseTestVersion?> selectVersion,
            bool includeHidden,
            CancellationToken cancellationToken)
        {
            var selected = tests
                .Select(test => (Test: test, Version: selectVersion(test)))
                .Where(item => item.Version is not null)
                .Select(item => (item.Test, Version: item.Version!))
                .ToList();

            if (selected.Count == 0)
                return [];

            var versionIds = selected.Select(item => item.Version.Id).ToArray();
            var detailsById = versionIds.Length == 0
                ? new Dictionary<Guid, CourseTestVersion>()
                : await LoadVersionDetails(includeHidden)
                    .Where(version => versionIds.Contains(version.Id))
                    .ToDictionaryAsync(version => version.Id, cancellationToken);

            var mapped = selected
                .Where(item => detailsById.ContainsKey(item.Version.Id))
                .Select(item => CourseTestMapper.ToDto(
                    item.Test,
                    detailsById[item.Version.Id],
                    includeHidden))
                .ToList();

            return mapped
                .OrderByDescending(test => test.Deadline)
                .ToList();
        }

        private IQueryable<CourseTest> LoadTestCatalog()
        {
            return _dbContext.Tests
                .AsNoTracking()
                .Include(test => test.Versions);
        }

        private IQueryable<CourseTestVersion> LoadVersionDetails(bool includeHidden)
        {
            if (includeHidden)
            {
                return _dbContext.TestVersions
                    .AsNoTracking()
                    .Include(version => version.Tasks)
                    .ThenInclude(task => task.Options);
            }

            return _dbContext.TestVersions
                .AsNoTracking()
                .Include(version => version.Tasks
                    .Where(task => !task.IsHidden))
                .ThenInclude(task => task.Options);
        }

        private Task<CourseTest?> GetTeacherOwnedActiveAggregateAsync(
            Guid testId,
            string teacherUserId,
            CancellationToken cancellationToken)
        {
            return _dbContext.Tests
                .Include(test => test.Versions.Where(version =>
                    version.VersionSlot == CourseTestStatus.Draft
                    || version.VersionSlot == CourseTestStatus.Published))
                .ThenInclude(version => version.Tasks)
                .ThenInclude(task => task.Options)
                .SingleOrDefaultAsync(
                    test => test.Id == testId
                        && test.TeacherUserId == teacherUserId,
                    cancellationToken);
        }

        private Task<CourseTest?> GetTeacherOwnedVersionCatalogTrackingAsync(
            Guid testId,
            string teacherUserId,
            CancellationToken cancellationToken)
        {
            return _dbContext.Tests
                .Include(test => test.Versions)
                .SingleOrDefaultAsync(
                    test => test.Id == testId
                        && test.TeacherUserId == teacherUserId,
                    cancellationToken);
        }

        private async Task<(CourseTest? Test, CourseTestVersion? Draft)> GetEditableDraftAsync(
            Guid testId,
            string teacherUserId,
            CancellationToken cancellationToken)
        {
            var test = await GetTeacherOwnedActiveAggregateAsync(
                testId,
                teacherUserId,
                cancellationToken);
            return (test, test is null ? null : GetDraftVersion(test));
        }

        private async Task<bool> TrySaveDraftMutationAsync(
            CourseTestVersion draft,
            CancellationToken cancellationToken)
        {
            draft.ContentRevision = checked(draft.ContentRevision + 1);
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                return false;
            }
        }

        private static CourseTestVersion? GetDraftVersion(CourseTest test)
        {
            return test.Versions.SingleOrDefault(
                version => version.Status == CourseTestStatus.Draft);
        }

        private static CourseTestVersion? GetPublishedVersion(CourseTest test)
        {
            return test.Versions.SingleOrDefault(
                version => version.Status == CourseTestStatus.Published);
        }

        private static CourseTestVersion CreateVersion(
            Guid testId,
            int versionNumber,
            CreateTestRequest request,
            DateTimeOffset createdAt)
        {
            var version = new CourseTestVersion
            {
                Id = Guid.NewGuid(),
                CourseTestId = testId,
                VersionNumber = versionNumber,
                Status = CourseTestStatus.Draft,
                CreatedAt = createdAt
            };
            ApplyTestDetails(version, request);
            return version;
        }

        private static CourseTestVersion CloneAsDraft(
            CourseTestVersion source,
            int versionNumber,
            DateTimeOffset createdAt)
        {
            var draft = new CourseTestVersion
            {
                Id = Guid.NewGuid(),
                CourseTestId = source.CourseTestId,
                VersionNumber = versionNumber,
                Status = CourseTestStatus.Draft,
                Title = source.Title,
                Subject = source.Subject,
                Deadline = source.Deadline,
                TimeLimitMinutes = source.TimeLimitMinutes,
                Summary = source.Summary,
                LlmModelKey = source.LlmModelKey,
                CreatedAt = createdAt
            };

            foreach (var sourceTask in source.Tasks
                         .OrderBy(task => task.CreatedAt)
                         .ThenBy(task => task.Id))
            {
                var taskId = Guid.NewGuid();
                draft.Tasks.Add(new TestTask
                {
                    Id = taskId,
                    CourseTestVersionId = draft.Id,
                    Type = sourceTask.Type,
                    CheckMode = sourceTask.CheckMode,
                    Title = sourceTask.Title,
                    Prompt = sourceTask.Prompt,
                    MaxPoints = sourceTask.MaxPoints,
                    WrongAnswerPenalty = sourceTask.WrongAnswerPenalty,
                    IsHidden = sourceTask.IsHidden,
                    CreatedAt = sourceTask.CreatedAt,
                    Options = sourceTask.Options
                        .OrderBy(option => option.Id)
                        .Select(option => new AnswerOption
                        {
                            Id = Guid.NewGuid(),
                            TestTaskId = taskId,
                            Text = option.Text,
                            IsCorrect = option.IsCorrect
                        })
                        .ToList()
                });
            }

            return draft;
        }

        private static void ApplyTestDetails(
            CourseTestVersion version,
            CreateTestRequest request)
        {
            version.Title = request.Title.Trim();
            version.Subject = request.Subject.Trim();
            version.Deadline = GetRequiredDeadline(request);
            version.TimeLimitMinutes = request.TimeLimitMinutes;
            version.Summary = request.Summary?.Trim() ?? string.Empty;
            version.LlmModelKey = request.LlmModelKey?.Trim() ?? string.Empty;
        }

        private static TestTask CreateTask(Guid versionId, CreateTaskRequest request)
        {
            var taskId = Guid.NewGuid();
            return new TestTask
            {
                Id = taskId,
                CourseTestVersionId = versionId,
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
                    ? []
                    : CreateOptions(taskId, request)
            };
        }

        private static List<AnswerOption> CreateOptions(
            Guid taskId,
            CreateTaskRequest request)
        {
            var correctOptionIndexes = (request.CorrectOptionIndexes ?? [])
                .Distinct()
                .ToHashSet();

            return (request.Options ?? []).Select((option, index) => new AnswerOption
            {
                Id = Guid.NewGuid(),
                TestTaskId = taskId,
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

        private static string? ValidateForPublishing(
            CourseTestVersion version,
            DateTimeOffset now)
        {
            if (version.Deadline <= now)
                return "Deadline must be in the future before the version can be published.";

            var visibleTasks = version.Tasks.Where(task => !task.IsHidden).ToList();
            if (visibleTasks.Count == 0)
                return "At least one visible task is required before publication.";

            if (visibleTasks.Any(task => !Enum.IsDefined(task.Type)
                    || !Enum.IsDefined(task.CheckMode)))
            {
                return "Every visible task must use a supported task type and review mode.";
            }

            if (visibleTasks.Any(task => task.CheckMode == TestTaskCheckMode.Llm)
                && string.IsNullOrWhiteSpace(version.LlmModelKey))
            {
                return "LlmModelKey is required when a visible task uses LLM review.";
            }

            return null;
        }

        private static void EnsureTeacherUserId(string teacherUserId)
        {
            if (string.IsNullOrWhiteSpace(teacherUserId))
                throw new ArgumentException("Teacher user id is required.", nameof(teacherUserId));
        }

        private static DateTimeOffset GetRequiredDeadline(CreateTestRequest request)
        {
            return request.Deadline
                ?? throw new ArgumentException("Deadline is required.", nameof(request));
        }
    }
}

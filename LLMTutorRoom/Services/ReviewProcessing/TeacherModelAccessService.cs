using System.Data;
using LLMTutorRoom.Data;
using LLMTutorRoom.DTOs;
using LLMTutorRoom.Models;
using Microsoft.EntityFrameworkCore;

namespace LLMTutorRoom.Services.ReviewProcessing
{
    public sealed class TeacherModelAccessService
    {
        private readonly TutorRoomDbContext _dbContext;
        private readonly LlmGatewayModelCatalogClient _modelCatalogClient;

        public TeacherModelAccessService(
            TutorRoomDbContext dbContext,
            LlmGatewayModelCatalogClient modelCatalogClient)
        {
            _dbContext = dbContext;
            _modelCatalogClient = modelCatalogClient;
        }

        public Task<List<LlmModelCatalogItemResponse>> GetModelCatalogAsync(
            CancellationToken cancellationToken)
        {
            return _modelCatalogClient.GetModelsAsync(cancellationToken);
        }

        public async Task<List<TeacherModelAccessResponse>> GetTeacherAccessAsync(
            string teacherUserId,
            bool includeDisabled,
            CancellationToken cancellationToken)
        {
            var query = _dbContext.TeacherModelAccesses
                .AsNoTracking()
                .Where(access => access.TeacherUserId == teacherUserId);

            if (!includeDisabled)
                query = query.Where(access => access.IsEnabled);

            var accesses = await query
                .OrderBy(access => access.ModelKey)
                .ToListAsync(cancellationToken);
            var catalog = await GetCatalogByKeyAsync(cancellationToken);
            var responses = new List<TeacherModelAccessResponse>();

            foreach (var access in accesses)
            {
                if (!includeDisabled
                    && (!catalog.TryGetValue(access.ModelKey, out var model)
                        || !model.HasEnabledDeployment))
                {
                    continue;
                }

                responses.Add(await ToResponseAsync(
                    access,
                    catalog,
                    cancellationToken));
            }

            return responses;
        }

        public async Task<TeacherModelAccessResponse?> UpsertTeacherAccessAsync(
            string teacherUserId,
            string modelKey,
            UpsertTeacherModelAccessRequest request,
            CancellationToken cancellationToken)
        {
            var catalog = await GetCatalogByKeyAsync(cancellationToken);
            if (!catalog.ContainsKey(modelKey))
                return null;

            var access = await _dbContext.TeacherModelAccesses
                .SingleOrDefaultAsync(
                    item => item.TeacherUserId == teacherUserId && item.ModelKey == modelKey,
                    cancellationToken);

            if (access is null)
            {
                access = new TeacherModelAccess
                {
                    TeacherUserId = teacherUserId,
                    ModelKey = modelKey
                };
                _dbContext.TeacherModelAccesses.Add(access);
            }

            access.IsEnabled = request.IsEnabled;
            access.PeriodSeconds = request.PeriodSeconds;
            access.MaxChecks = request.MaxChecks;
            access.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            return await ToResponseAsync(
                access,
                catalog,
                cancellationToken);
        }

        public async Task<bool> DeleteTeacherAccessAsync(
            string teacherUserId,
            string modelKey,
            CancellationToken cancellationToken)
        {
            var access = await _dbContext.TeacherModelAccesses
                .SingleOrDefaultAsync(
                    item => item.TeacherUserId == teacherUserId && item.ModelKey == modelKey,
                    cancellationToken);

            if (access is null)
                return false;

            _dbContext.TeacherModelAccesses.Remove(access);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<ModelQuotaConsumptionResult> TryConsumeChecksAsync(
            string teacherUserId,
            string modelKey,
            int checkCount,
            CancellationToken cancellationToken)
        {
            if (checkCount <= 0)
                return CreateInvalidConsumeResult(
                    ModelQuotaConsumptionStatus.InvalidCheckCount,
                    teacherUserId,
                    modelKey,
                    checkCount);

            await using var transaction = _dbContext.Database.IsRelational()
                ? await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                : null;

            var access = await _dbContext.TeacherModelAccesses
                .SingleOrDefaultAsync(
                    item => item.TeacherUserId == teacherUserId && item.ModelKey == modelKey,
                    cancellationToken);

            if (access is null
                || !access.IsEnabled
                || access.PeriodSeconds <= 0
                || access.MaxChecks <= 0)
            {
                return CreateInvalidConsumeResult(
                    ModelQuotaConsumptionStatus.AccessNotFound,
                    teacherUserId,
                    modelKey,
                    checkCount);
            }

            var now = DateTimeOffset.UtcNow;
            var periodStart = GetPeriodStart(now, access.PeriodSeconds);
            var usage = await _dbContext.TeacherModelUsages
                .SingleOrDefaultAsync(
                    item => item.TeacherUserId == teacherUserId
                        && item.ModelKey == modelKey
                        && item.PeriodStart == periodStart
                        && item.PeriodSeconds == access.PeriodSeconds,
                    cancellationToken);

            if (usage is null)
            {
                usage = new TeacherModelUsage
                {
                    TeacherUserId = teacherUserId,
                    ModelKey = modelKey,
                    PeriodStart = periodStart,
                    PeriodSeconds = access.PeriodSeconds
                };
                _dbContext.TeacherModelUsages.Add(usage);
            }

            var remainingChecks = access.MaxChecks - usage.UsedChecks;
            if (remainingChecks < checkCount)
            {
                return CreateConsumeResult(
                    ModelQuotaConsumptionStatus.LimitExceeded,
                    teacherUserId,
                    modelKey,
                    checkCount,
                    usage.UsedChecks,
                    Math.Max(0, remainingChecks),
                    periodStart,
                    access.PeriodSeconds);
            }

            usage.UsedChecks += checkCount;
            usage.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);

            return CreateConsumeResult(
                ModelQuotaConsumptionStatus.Allowed,
                teacherUserId,
                modelKey,
                checkCount,
                usage.UsedChecks,
                access.MaxChecks - usage.UsedChecks,
                periodStart,
                access.PeriodSeconds);
        }

        private async Task<Dictionary<string, LlmModelCatalogItemResponse>> GetCatalogByKeyAsync(
            CancellationToken cancellationToken)
        {
            var models = await _modelCatalogClient.GetModelsAsync(cancellationToken);
            return models.ToDictionary(model => model.Key);
        }

        private async Task<TeacherModelAccessResponse> ToResponseAsync(
            TeacherModelAccess access,
            IReadOnlyDictionary<string, LlmModelCatalogItemResponse> catalog,
            CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            var periodStart = GetPeriodStart(now, access.PeriodSeconds);
            var usage = await _dbContext.TeacherModelUsages
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.TeacherUserId == access.TeacherUserId
                        && item.ModelKey == access.ModelKey
                        && item.PeriodStart == periodStart
                        && item.PeriodSeconds == access.PeriodSeconds,
                    cancellationToken);
            var usedChecks = usage?.UsedChecks ?? 0;
            catalog.TryGetValue(access.ModelKey, out var model);

            return new TeacherModelAccessResponse(
                access.TeacherUserId,
                access.ModelKey,
                model?.DisplayName ?? access.ModelKey,
                access.IsEnabled,
                model?.HasEnabledDeployment ?? false,
                access.PeriodSeconds,
                access.MaxChecks,
                usedChecks,
                Math.Max(0, access.MaxChecks - usedChecks),
                periodStart,
                periodStart.AddSeconds(access.PeriodSeconds));
        }

        private static ModelQuotaConsumptionResult CreateInvalidConsumeResult(
            ModelQuotaConsumptionStatus status,
            string teacherUserId,
            string modelKey,
            int requestedChecks)
        {
            var now = DateTimeOffset.UtcNow;
            return new ModelQuotaConsumptionResult(
                status,
                requestedChecks,
                0,
                0,
                now);
        }

        private static ModelQuotaConsumptionResult CreateConsumeResult(
            ModelQuotaConsumptionStatus status,
            string teacherUserId,
            string modelKey,
            int requestedChecks,
            int usedChecks,
            int remainingChecks,
            DateTimeOffset periodStart,
            int periodSeconds)
        {
            return new ModelQuotaConsumptionResult(
                status,
                requestedChecks,
                usedChecks,
                remainingChecks,
                periodStart.AddSeconds(periodSeconds));
        }

        public static DateTimeOffset GetPeriodStart(
            DateTimeOffset now,
            int periodSeconds)
        {
            var unixSeconds = now.ToUnixTimeSeconds();
            var periodStartUnixSeconds = unixSeconds - unixSeconds % periodSeconds;
            return DateTimeOffset.FromUnixTimeSeconds(periodStartUnixSeconds);
        }
    }
}

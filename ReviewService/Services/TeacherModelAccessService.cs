using System.Data;
using Microsoft.EntityFrameworkCore;
using ReviewService.Contracts.Requests;
using ReviewService.Contracts.Responses;
using ReviewService.Data;
using ReviewService.Enums;
using ReviewService.Interfaces;
using ReviewService.Models.ModelAccess;

namespace ReviewService.Services;

public sealed class TeacherModelAccessService(
    ReviewDbContext dbContext,
    ILlmGatewayModelCatalogClient modelCatalogClient) : ITeacherModelAccessService
{
    public Task<List<LlmModelCatalogItemResponse>> GetModelCatalogAsync(
        CancellationToken cancellationToken)
    {
        return modelCatalogClient.GetModelsAsync(cancellationToken);
    }

    public async Task<List<TeacherModelAccessResponse>> GetTeacherAccessAsync(
        string teacherUserId,
        bool includeDisabled,
        CancellationToken cancellationToken)
    {
        var query = dbContext.TeacherModelAccesses
            .AsNoTracking()
            .Where(access => access.TeacherUserId == teacherUserId);
        if (!includeDisabled)
            query = query.Where(access => access.IsEnabled);

        var accesses = await query
            .OrderBy(access => access.ModelKey)
            .ToListAsync(cancellationToken);
        var catalog = (await GetModelCatalogAsync(cancellationToken))
            .ToDictionary(model => model.Key, StringComparer.Ordinal);
        var responses = new List<TeacherModelAccessResponse>();

        foreach (var access in accesses)
        {
            if (!includeDisabled
                && (!catalog.TryGetValue(access.ModelKey, out var model)
                    || !model.HasEnabledDeployment))
            {
                continue;
            }

            responses.Add(await ToResponseAsync(access, catalog, cancellationToken));
        }

        return responses;
    }

    public async Task<TeacherModelAccessResponse?> UpsertTeacherAccessAsync(
        string teacherUserId,
        string modelKey,
        UpsertTeacherModelAccessRequest request,
        CancellationToken cancellationToken)
    {
        modelKey = modelKey.Trim();
        var catalog = (await GetModelCatalogAsync(cancellationToken))
            .ToDictionary(model => model.Key, StringComparer.Ordinal);
        if (!catalog.ContainsKey(modelKey))
            return null;

        var now = DateTimeOffset.UtcNow;
        var access = await dbContext.TeacherModelAccesses.SingleOrDefaultAsync(
            item => item.TeacherUserId == teacherUserId && item.ModelKey == modelKey,
            cancellationToken);
        var isNew = access is null;
        if (access is null)
        {
            access = CreateAccess(teacherUserId, modelKey, now);
            dbContext.TeacherModelAccesses.Add(access);
        }

        ApplyAccessSettings(access, request, now);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (isNew || exception is DbUpdateConcurrencyException)
        {
            dbContext.Entry(access).State = EntityState.Detached;
            now = DateTimeOffset.UtcNow;
            access = await dbContext.TeacherModelAccesses.SingleOrDefaultAsync(
                item => item.TeacherUserId == teacherUserId && item.ModelKey == modelKey,
                cancellationToken);
            if (access is null)
            {
                access = CreateAccess(teacherUserId, modelKey, now);
                dbContext.TeacherModelAccesses.Add(access);
            }

            ApplyAccessSettings(access, request, now);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return await ToResponseAsync(access, catalog, cancellationToken);
    }

    public async Task<bool> DeleteTeacherAccessAsync(
        string teacherUserId,
        string modelKey,
        CancellationToken cancellationToken)
    {
        var access = await dbContext.TeacherModelAccesses.SingleOrDefaultAsync(
            item => item.TeacherUserId == teacherUserId && item.ModelKey == modelKey,
            cancellationToken);
        if (access is null)
            return false;

        dbContext.TeacherModelAccesses.Remove(access);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ModelQuotaConsumptionResult> TryConsumeChecksAsync(
        string teacherUserId,
        string modelKey,
        int checkCount,
        CancellationToken cancellationToken)
    {
        if (checkCount <= 0)
            return InvalidResult(ModelQuotaConsumptionStatus.InvalidCheckCount, checkCount);

        await using var transaction = dbContext.Database.IsRelational()
            && dbContext.Database.CurrentTransaction is null
                ? await dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken)
                : null;

        var access = await dbContext.TeacherModelAccesses.SingleOrDefaultAsync(
            item => item.TeacherUserId == teacherUserId && item.ModelKey == modelKey,
            cancellationToken);
        if (access is null || !access.IsEnabled || access.PeriodSeconds <= 0 || access.MaxChecks <= 0)
            return InvalidResult(ModelQuotaConsumptionStatus.AccessNotFound, checkCount);

        var now = DateTimeOffset.UtcNow;
        var periodStart = GetPeriodStart(now, access.PeriodSeconds);
        var usage = await dbContext.TeacherModelUsages.SingleOrDefaultAsync(
            item => item.TeacherUserId == teacherUserId
                && item.ModelKey == modelKey
                && item.PeriodStart == periodStart
                && item.PeriodSeconds == access.PeriodSeconds,
            cancellationToken);
        var usedChecks = usage?.UsedChecks ?? 0;
        var remainingChecks = access.MaxChecks - usedChecks;
        if (remainingChecks < checkCount)
        {
            return new ModelQuotaConsumptionResult(
                ModelQuotaConsumptionStatus.LimitExceeded,
                checkCount,
                usedChecks,
                Math.Max(0, remainingChecks),
                periodStart.AddSeconds(access.PeriodSeconds));
        }

        if (usage is null)
        {
            usage = new TeacherModelUsage
            {
                TeacherUserId = teacherUserId,
                ModelKey = modelKey,
                PeriodStart = periodStart,
                PeriodSeconds = access.PeriodSeconds,
                CreatedAt = now,
                UpdatedAt = now
            };
            dbContext.TeacherModelUsages.Add(usage);
        }

        usage.UsedChecks += checkCount;
        usage.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        return new ModelQuotaConsumptionResult(
            ModelQuotaConsumptionStatus.Allowed,
            checkCount,
            usage.UsedChecks,
            access.MaxChecks - usage.UsedChecks,
            periodStart.AddSeconds(access.PeriodSeconds));
    }

    public static DateTimeOffset GetPeriodStart(DateTimeOffset now, int periodSeconds)
    {
        var unixSeconds = now.ToUnixTimeSeconds();
        return DateTimeOffset.FromUnixTimeSeconds(unixSeconds - unixSeconds % periodSeconds);
    }

    private static TeacherModelAccess CreateAccess(
        string teacherUserId,
        string modelKey,
        DateTimeOffset now)
    {
        return new TeacherModelAccess
        {
            TeacherUserId = teacherUserId,
            ModelKey = modelKey,
            CreatedAt = now
        };
    }

    private static void ApplyAccessSettings(
        TeacherModelAccess access,
        UpsertTeacherModelAccessRequest request,
        DateTimeOffset now)
    {
        access.IsEnabled = request.IsEnabled;
        access.PeriodSeconds = request.PeriodSeconds;
        access.MaxChecks = request.MaxChecks;
        access.UpdatedAt = now;
    }

    private async Task<TeacherModelAccessResponse> ToResponseAsync(
        TeacherModelAccess access,
        Dictionary<string, LlmModelCatalogItemResponse> catalog,
        CancellationToken cancellationToken)
    {
        var periodStart = GetPeriodStart(DateTimeOffset.UtcNow, access.PeriodSeconds);
        var usage = await dbContext.TeacherModelUsages
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

    private ModelQuotaConsumptionResult InvalidResult(
        ModelQuotaConsumptionStatus status,
        int requestedChecks)
    {
        return new ModelQuotaConsumptionResult(
            status,
            requestedChecks,
            0,
            0,
            DateTimeOffset.UtcNow);
    }
}

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
            .OrderBy(access => access.ModelId)
            .ToListAsync(cancellationToken);
        var catalog = (await GetModelCatalogAsync(cancellationToken))
            .ToDictionary(model => model.Id);
        var responses = new List<TeacherModelAccessResponse>();

        foreach (var access in accesses)
        {
            if (!includeDisabled
                && (!catalog.TryGetValue(access.ModelId, out var model)
                    || model.IsArchived || !model.HasEnabledDeployment))
            {
                continue;
            }

            responses.Add(await ToResponseAsync(access, catalog, cancellationToken));
        }

        return responses;
    }

    public async Task<TeacherModelAccessResponse?> UpsertTeacherAccessAsync(
        string teacherUserId,
        int modelId,
        UpsertTeacherModelAccessRequest request,
        CancellationToken cancellationToken)
    {
        var catalog = (await GetModelCatalogAsync(cancellationToken))
            .ToDictionary(model => model.Id);
        if (!catalog.TryGetValue(modelId, out var selectedModel))
            return null;

        var now = DateTimeOffset.UtcNow;
        var access = await dbContext.TeacherModelAccesses.SingleOrDefaultAsync(
            item => item.TeacherUserId == teacherUserId && item.ModelId == modelId,
            cancellationToken);
        var isNew = access is null;
        if (isNew && selectedModel.IsArchived)
            return null;
        if (access is null)
        {
            access = CreateAccess(teacherUserId, modelId, now);
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
                item => item.TeacherUserId == teacherUserId && item.ModelId == modelId,
                cancellationToken);
            if (access is null)
            {
                access = CreateAccess(teacherUserId, modelId, now);
                dbContext.TeacherModelAccesses.Add(access);
            }

            ApplyAccessSettings(access, request, now);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return await ToResponseAsync(access, catalog, cancellationToken);
    }

    public async Task<bool> DeleteTeacherAccessAsync(
        string teacherUserId,
        int modelId,
        CancellationToken cancellationToken)
    {
        var access = await dbContext.TeacherModelAccesses.SingleOrDefaultAsync(
            item => item.TeacherUserId == teacherUserId && item.ModelId == modelId,
            cancellationToken);
        if (access is null)
            return false;

        dbContext.TeacherModelAccesses.Remove(access);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ModelQuotaConsumptionResult> TryConsumeChecksAsync(
        string teacherUserId,
        int modelId,
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
            item => item.TeacherUserId == teacherUserId && item.ModelId == modelId,
            cancellationToken);
        if (access is null || !access.IsEnabled || access.PeriodSeconds <= 0 || access.MaxChecks <= 0)
            return InvalidResult(ModelQuotaConsumptionStatus.AccessNotFound, checkCount);

        var now = DateTimeOffset.UtcNow;
        var periodStart = GetPeriodStart(now, access.PeriodSeconds);
        var usage = await dbContext.TeacherModelUsages.SingleOrDefaultAsync(
            item => item.TeacherUserId == teacherUserId
                && item.ModelId == modelId
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
                ModelId = modelId,
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
        int modelId,
        DateTimeOffset now)
    {
        return new TeacherModelAccess
        {
            TeacherUserId = teacherUserId,
            ModelId = modelId,
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
        Dictionary<int, LlmModelCatalogItemResponse> catalog,
        CancellationToken cancellationToken)
    {
        var periodStart = GetPeriodStart(DateTimeOffset.UtcNow, access.PeriodSeconds);
        var usage = await dbContext.TeacherModelUsages
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.TeacherUserId == access.TeacherUserId
                    && item.ModelId == access.ModelId
                    && item.PeriodStart == periodStart
                    && item.PeriodSeconds == access.PeriodSeconds,
                cancellationToken);
        var usedChecks = usage?.UsedChecks ?? 0;
        catalog.TryGetValue(access.ModelId, out var model);

        return new TeacherModelAccessResponse(
            access.TeacherUserId,
            access.ModelId,
            model?.DisplayName ?? $"Модель #{access.ModelId}",
            access.IsEnabled,
            model is { IsArchived: false, HasEnabledDeployment: true },
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

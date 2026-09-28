using LLMGateway.Data;
using LLMGateway.Data.Models;
using LLMGateway.DTOs.Models;
using Microsoft.EntityFrameworkCore;

namespace LLMGateway.Services;

public sealed class ModelManagementService(AppDbContext db, IDeploymentProbe probe) : IModelManagementService
{
    private IQueryable<Model> Models => db.Models.AsNoTracking()
        .Include(model => model.Deployments).ThenInclude(deployment => deployment.RateLimitRules);

    public async Task<List<ModelResponse>> GetModelsAsync(CancellationToken ct) =>
        (await Models.OrderBy(model => model.DisplayName).ToListAsync(ct)).Select(Map).ToList();

    public Task<List<ModelCatalogItemResponse>> GetCatalogAsync(CancellationToken ct) => db.Models.AsNoTracking()
        .OrderBy(model => model.DisplayName)
        .Select(model => new ModelCatalogItemResponse(model.Id, model.Key, model.DisplayName,
            model.IsArchived, model.Deployments.Any(deployment => deployment.IsEnabled)))
        .ToListAsync(ct);

    public async Task<ModelResponse?> GetModelAsync(int modelId, CancellationToken ct) =>
        await Models.SingleOrDefaultAsync(item => item.Id == modelId, ct) is { } model ? Map(model) : null;

    public async Task<ModelResponse> CreateModelAsync(CreateModelRequest request, CancellationToken ct)
    {
        await EnsureUniqueKeyAsync(request.Key.Trim(), null, ct);
        var model = new Model { Key = request.Key.Trim(), DisplayName = request.DisplayName.Trim(),
            Description = request.Description?.Trim(), IsArchived = request.IsArchived };
        db.Models.Add(model);
        await db.SaveChangesAsync(ct);
        return Map(model);
    }

    public async Task<ModelResponse?> UpdateModelAsync(int modelId, UpdateModelRequest request, CancellationToken ct)
    {
        var model = await db.Models.Include(item => item.Deployments).ThenInclude(item => item.RateLimitRules)
            .SingleOrDefaultAsync(item => item.Id == modelId, ct);
        if (model is null) return null;
        await EnsureUniqueKeyAsync(request.Key.Trim(), modelId, ct);
        model.Key = request.Key.Trim();
        model.DisplayName = request.DisplayName.Trim();
        model.Description = request.Description?.Trim();
        model.IsArchived = request.IsArchived;
        model.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Map(model);
    }

    private async Task EnsureUniqueKeyAsync(string key, int? modelId, CancellationToken ct)
    {
        if (await db.Models.AnyAsync(model => model.Key == key && model.Id != modelId, ct))
            throw new ModelKeyConflictException();
    }

    public async Task<ModelDeploymentResponse?> GetDeploymentAsync(int deploymentId, CancellationToken ct) =>
        await db.ModelDeployments.AsNoTracking().Include(item => item.RateLimitRules)
            .SingleOrDefaultAsync(item => item.Id == deploymentId, ct) is { } deployment ? Map(deployment) : null;

    public async Task<ModelDeploymentResponse> CreateDeploymentAsync(int modelId, CreateModelDeploymentRequest request, CancellationToken ct)
    {
        if (!await db.Models.AnyAsync(model => model.Id == modelId, ct)) throw new KeyNotFoundException();
        var deployment = new ModelDeployment { ModelId = modelId, Name = request.Name.Trim(),
            Endpoint = request.Endpoint.Trim().TrimEnd('/'), ApiKey = NormalizeSecret(request.ApiKey),
            ProviderModelId = request.ProviderModelId.Trim(), IsEnabled = request.IsEnabled,
            Priority = request.Priority, MaxConcurrentRequests = request.MaxConcurrentRequests };
        db.ModelDeployments.Add(deployment);
        await db.SaveChangesAsync(ct);
        return Map(deployment);
    }

    public async Task<ModelDeploymentResponse?> UpdateDeploymentAsync(int deploymentId, UpdateModelDeploymentRequest request, CancellationToken ct)
    {
        if (request.ClearApiKey && !string.IsNullOrWhiteSpace(request.ApiKey))
            throw new ArgumentException("Choose either replacing or clearing the API key.");
        if (request.ApiKey is not null && string.IsNullOrWhiteSpace(request.ApiKey))
            throw new ArgumentException("Use ClearApiKey to remove the saved secret.");
        var deployment = await db.ModelDeployments.Include(item => item.RateLimitRules)
            .SingleOrDefaultAsync(item => item.Id == deploymentId, ct);
        if (deployment is null) return null;
        deployment.Name = request.Name.Trim();
        deployment.Endpoint = request.Endpoint.Trim().TrimEnd('/');
        deployment.ProviderModelId = request.ProviderModelId.Trim();
        deployment.IsEnabled = request.IsEnabled;
        deployment.Priority = request.Priority;
        deployment.MaxConcurrentRequests = request.MaxConcurrentRequests;
        if (request.ClearApiKey) deployment.ApiKey = null;
        else if (request.ApiKey is not null) deployment.ApiKey = NormalizeSecret(request.ApiKey);
        deployment.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Map(deployment);
    }

    public async Task<bool> DeleteDeploymentAsync(int deploymentId, CancellationToken ct)
    {
        var deployment = await db.ModelDeployments.Include(item => item.RateLimitRules)
            .SingleOrDefaultAsync(item => item.Id == deploymentId, ct);
        if (deployment is null) return false;
        db.ModelDeployments.Remove(deployment);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<DeploymentCheckResponse?> CheckDeploymentAsync(int deploymentId, CancellationToken ct)
    {
        var deployment = await db.ModelDeployments.AsNoTracking().SingleOrDefaultAsync(item => item.Id == deploymentId, ct);
        return deployment is null ? null : await probe.CheckAsync(deployment, ct);
    }

    public async Task<RateLimitRuleResponse?> GetRateLimitAsync(int ruleId, CancellationToken ct) =>
        await db.ModelRateLimitRules.AsNoTracking().SingleOrDefaultAsync(item => item.Id == ruleId, ct) is { } rule ? Map(rule) : null;

    public async Task<RateLimitRuleResponse> CreateRateLimitAsync(int deploymentId, CreateRateLimitRuleRequest request, CancellationToken ct)
    {
        var deployment = await db.ModelDeployments.SingleOrDefaultAsync(item => item.Id == deploymentId, ct)
            ?? throw new KeyNotFoundException();
        var rule = new ModelRateLimitRule { DeploymentId = deploymentId,
            WindowSeconds = request.WindowSeconds, MaxRequests = request.MaxRequests };
        db.ModelRateLimitRules.Add(rule);
        deployment.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Map(rule);
    }

    public async Task<RateLimitRuleResponse?> UpdateRateLimitAsync(int ruleId, UpdateRateLimitRuleRequest request, CancellationToken ct)
    {
        var rule = await db.ModelRateLimitRules.Include(item => item.Deployment).SingleOrDefaultAsync(item => item.Id == ruleId, ct);
        if (rule is null) return null;
        rule.WindowSeconds = request.WindowSeconds;
        rule.MaxRequests = request.MaxRequests;
        rule.Deployment.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Map(rule);
    }

    public async Task<bool> DeleteRateLimitAsync(int ruleId, CancellationToken ct)
    {
        var rule = await db.ModelRateLimitRules.Include(item => item.Deployment).SingleOrDefaultAsync(item => item.Id == ruleId, ct);
        if (rule is null) return false;
        rule.Deployment.UpdatedAt = DateTime.UtcNow;
        db.ModelRateLimitRules.Remove(rule);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static string? NormalizeSecret(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static ModelResponse Map(Model model) => new(model.Id, model.Key, model.DisplayName,
        model.Description, model.IsArchived, model.Deployments.OrderBy(item => item.Priority).ThenBy(item => item.Id).Select(Map).ToList());
    private static ModelDeploymentResponse Map(ModelDeployment deployment) => new(deployment.Id, deployment.ModelId,
        deployment.Name, deployment.Endpoint, deployment.ProviderModelId, !string.IsNullOrEmpty(deployment.ApiKey),
        deployment.IsEnabled, deployment.Priority, deployment.MaxConcurrentRequests,
        new DateTimeOffset(deployment.UpdatedAt, TimeSpan.Zero), deployment.RateLimitRules.OrderBy(item => item.Id).Select(Map).ToList());
    private static RateLimitRuleResponse Map(ModelRateLimitRule rule) => new(rule.Id, rule.DeploymentId, rule.WindowSeconds, rule.MaxRequests);
}

public sealed class ModelKeyConflictException : Exception { }

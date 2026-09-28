using ReviewService.Contracts.Requests;
using ReviewService.Contracts.Responses;
using ReviewService.Models.ModelAccess;

namespace ReviewService.Interfaces;

public interface ITeacherModelAccessService
{
    Task<List<LlmModelCatalogItemResponse>> GetModelCatalogAsync(CancellationToken cancellationToken);

    Task<List<TeacherModelAccessResponse>> GetTeacherAccessAsync(
        string teacherUserId,
        bool includeDisabled,
        CancellationToken cancellationToken);

    Task<TeacherModelAccessResponse?> UpsertTeacherAccessAsync(
        string teacherUserId,
        int modelId,
        UpsertTeacherModelAccessRequest request,
        CancellationToken cancellationToken);

    Task<bool> DeleteTeacherAccessAsync(
        string teacherUserId,
        int modelId,
        CancellationToken cancellationToken);

    Task<ModelQuotaConsumptionResult> TryConsumeChecksAsync(
        string teacherUserId,
        int modelId,
        int checkCount,
        CancellationToken cancellationToken);
}

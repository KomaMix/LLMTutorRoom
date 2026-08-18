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
        string modelKey,
        UpsertTeacherModelAccessRequest request,
        CancellationToken cancellationToken);

    Task<bool> DeleteTeacherAccessAsync(
        string teacherUserId,
        string modelKey,
        CancellationToken cancellationToken);

    Task<ModelQuotaConsumptionResult> TryConsumeChecksAsync(
        string teacherUserId,
        string modelKey,
        int checkCount,
        CancellationToken cancellationToken);
}

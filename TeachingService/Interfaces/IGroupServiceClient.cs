using GroupService.Contracts.Responses;

namespace TeachingService.Interfaces;

public interface IGroupServiceClient
{
    Task<StudyGroupResponse?> GetGroupAsync(Guid groupId, CancellationToken cancellationToken);
    Task<List<StudyGroupResponse>> GetStudentGroupsAsync(string studentUserId, CancellationToken cancellationToken);
}

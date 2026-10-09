using GroupService.Contracts.Responses;

namespace LLMTutorRoom.Interfaces;

public interface IGroupServiceClient
{
    Task<StudyGroupResponse?> GetGroupAsync(Guid groupId, CancellationToken cancellationToken);

    Task<List<StudyGroupResponse>> GetTeacherGroupsAsync(
        string teacherUserId,
        CancellationToken cancellationToken);

    Task<List<StudyGroupResponse>> GetStudentGroupsAsync(
        string studentUserId,
        CancellationToken cancellationToken);
}

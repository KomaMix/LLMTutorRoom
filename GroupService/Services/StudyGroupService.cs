using Microsoft.EntityFrameworkCore;
using GroupService.Contracts.Requests;
using GroupService.Contracts.Responses;
using GroupService.Data;
using GroupService.Enums;
using GroupService.Models;

namespace GroupService.Services;

public class StudyGroupService
{
    private const int MaxGroupNameLength = 200;
    private const int MaxUserIdLength = 450;
    private readonly GroupDbContext _dbContext;

    public StudyGroupService(GroupDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<StudyGroupResponse>> GetGroupsAsync(
        string? teacherUserId,
        CancellationToken cancellationToken)
    {
        var query = GroupQuery();
        if (teacherUserId is not null)
        {
            query = query.Where(group => group.TeacherUserId == teacherUserId);
        }

        var groups = await query
            .OrderBy(group => group.Name)
            .ThenBy(group => group.Id)
            .ToListAsync(cancellationToken);
        return groups.Select(ToResponse).ToList();
    }

    public async Task<StudyGroupResponse?> GetGroupAsync(
        Guid groupId,
        string? teacherUserId,
        CancellationToken cancellationToken)
    {
        var query = GroupQuery().Where(group => group.Id == groupId);
        if (teacherUserId is not null)
        {
            query = query.Where(group => group.TeacherUserId == teacherUserId);
        }

        var group = await query.SingleOrDefaultAsync(cancellationToken);
        return group is null ? null : ToResponse(group);
    }

    public async Task<List<StudyGroupResponse>> GetStudentGroupsAsync(
        string studentUserId,
        CancellationToken cancellationToken)
    {
        var groups = await GroupQuery()
            .Where(group => group.Students.Any(student => student.StudentUserId == studentUserId))
            .OrderBy(group => group.Name)
            .ThenBy(group => group.Id)
            .ToListAsync(cancellationToken);
        return groups.Select(ToResponse).ToList();
    }

    public async Task<Result<GroupOperationStatus, StudyGroupResponse>> CreateGroupAsync(
        CreateStudyGroupRequest request,
        string teacherUserId,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateGroup(request.Name, teacherUserId, request.StudentUserIds);
        if (validationError is not null)
        {
            return new Result<GroupOperationStatus, StudyGroupResponse>(
                GroupOperationStatus.ValidationFailed,
                error: validationError);
        }

        var now = DateTimeOffset.UtcNow;
        var group = new StudyGroup
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            TeacherUserId = teacherUserId.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
            Students = NormalizeStudentIds(request.StudentUserIds)
                .Select(studentUserId => new StudyGroupStudent
                {
                    StudentUserId = studentUserId,
                    AddedAt = now
                })
                .ToList()
        };

        _dbContext.StudyGroups.Add(group);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new Result<GroupOperationStatus, StudyGroupResponse>(
            GroupOperationStatus.Success,
            ToResponse(group));
    }

    public async Task<Result<GroupOperationStatus, StudyGroupResponse>> UpdateGroupAsync(
        Guid groupId,
        UpdateStudyGroupRequest request,
        string? teacherUserId,
        CancellationToken cancellationToken)
    {
        var nextTeacherUserId = teacherUserId ?? request.TeacherUserId;
        var validationError = ValidateGroup(request.Name, nextTeacherUserId, request.StudentUserIds);
        if (validationError is not null || request.Revision < 0)
        {
            return new Result<GroupOperationStatus, StudyGroupResponse>(
                GroupOperationStatus.ValidationFailed,
                error: validationError ?? "Revision must be non-negative.");
        }

        var query = _dbContext.StudyGroups
            .Include(group => group.Students)
            .Where(group => group.Id == groupId);
        if (teacherUserId is not null)
        {
            query = query.Where(group => group.TeacherUserId == teacherUserId);
        }

        var group = await query.SingleOrDefaultAsync(cancellationToken);
        if (group is null)
        {
            return new Result<GroupOperationStatus, StudyGroupResponse>(GroupOperationStatus.NotFound);
        }

        if (group.Revision != request.Revision)
        {
            return new Result<GroupOperationStatus, StudyGroupResponse>(
                GroupOperationStatus.Conflict,
                error: "The group was changed by another request.");
        }

        var now = DateTimeOffset.UtcNow;
        var nextStudentIds = NormalizeStudentIds(request.StudentUserIds).ToHashSet(StringComparer.Ordinal);
        group.Name = request.Name.Trim();
        group.TeacherUserId = nextTeacherUserId!.Trim();
        group.UpdatedAt = now;
        group.Revision = checked(group.Revision + 1);

        var removedStudents = group.Students
            .Where(student => !nextStudentIds.Contains(student.StudentUserId))
            .ToList();
        _dbContext.StudyGroupStudents.RemoveRange(removedStudents);
        foreach (var removedStudent in removedStudents)
        {
            group.Students.Remove(removedStudent);
        }

        var existingStudentIds = group.Students
            .Select(student => student.StudentUserId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var studentUserId in nextStudentIds.Where(id => !existingStudentIds.Contains(id)))
        {
            group.Students.Add(new StudyGroupStudent
            {
                StudentUserId = studentUserId,
                AddedAt = now
            });
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new Result<GroupOperationStatus, StudyGroupResponse>(
                GroupOperationStatus.Conflict,
                error: "The group was changed by another request.");
        }

        return new Result<GroupOperationStatus, StudyGroupResponse>(
            GroupOperationStatus.Success,
            ToResponse(group));
    }

    private IQueryable<StudyGroup> GroupQuery()
    {
        return _dbContext.StudyGroups
            .AsNoTracking()
            .Include(group => group.Students);
    }

    private static StudyGroupResponse ToResponse(StudyGroup group)
    {
        return new StudyGroupResponse
        {
            Id = group.Id,
            Name = group.Name,
            TeacherUserId = group.TeacherUserId,
            StudentUserIds = group.Students
                .OrderBy(student => student.StudentUserId)
                .Select(student => student.StudentUserId)
                .ToList(),
            CreatedAt = group.CreatedAt,
            UpdatedAt = group.UpdatedAt,
            Revision = group.Revision
        };
    }

    private static string? ValidateGroup(
        string? name,
        string? teacherUserId,
        IEnumerable<string>? studentUserIds)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Group name is required.";
        }

        if (name.Trim().Length > MaxGroupNameLength)
        {
            return $"Group name must not exceed {MaxGroupNameLength} characters.";
        }

        if (string.IsNullOrWhiteSpace(teacherUserId))
        {
            return "TeacherUserId is required.";
        }

        if (teacherUserId.Trim().Length > MaxUserIdLength)
        {
            return $"TeacherUserId must not exceed {MaxUserIdLength} characters.";
        }

        if (studentUserIds is null)
        {
            return "StudentUserIds is required.";
        }

        if (studentUserIds.Any(string.IsNullOrWhiteSpace))
        {
            return "StudentUserIds must not contain empty values.";
        }

        if (studentUserIds.Any(studentUserId => studentUserId.Trim().Length > MaxUserIdLength))
        {
            return $"StudentUserIds must not exceed {MaxUserIdLength} characters.";
        }

        return null;
    }

    private static List<string> NormalizeStudentIds(IEnumerable<string> studentUserIds)
    {
        return studentUserIds
            .Select(studentUserId => studentUserId.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(studentUserId => studentUserId, StringComparer.Ordinal)
            .ToList();
    }
}

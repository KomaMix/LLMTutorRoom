using Microsoft.EntityFrameworkCore;
using TeachingService.Contracts.Requests;
using TeachingService.Contracts.Responses;
using TeachingService.Data;
using TeachingService.Enums;
using TeachingService.Mappers;
using TeachingService.Models;

namespace TeachingService.Services;

public class StudyGroupService
{
    private const int MaxGroupNameLength = 200;
    private const int MaxUserIdLength = 450;
    private readonly TeachingDbContext _dbContext;

    public StudyGroupService(TeachingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<StudyGroupResponse>> GetGroupsAsync(
        string? teacherUserId,
        CancellationToken cancellationToken)
    {
        var query = GroupQuery();
        if (!string.IsNullOrWhiteSpace(teacherUserId))
        {
            query = query.Where(group => group.TeacherUserId == teacherUserId);
        }

        var groups = await query
            .OrderBy(group => group.Name)
            .ThenBy(group => group.Id)
            .ToListAsync(cancellationToken);

        return groups.Select(group => ToResponse(group, includeHidden: true)).ToList();
    }

    public async Task<StudyGroupResponse?> GetGroupAsync(
        Guid groupId,
        string? teacherUserId,
        bool includeHidden,
        CancellationToken cancellationToken)
    {
        var query = GroupQuery().Where(group => group.Id == groupId);
        if (!string.IsNullOrWhiteSpace(teacherUserId))
        {
            query = query.Where(group => group.TeacherUserId == teacherUserId);
        }

        var group = await query.SingleOrDefaultAsync(cancellationToken);
        return group is null ? null : ToResponse(group, includeHidden);
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
            ToResponse(group, includeHidden: true));
    }

    public async Task<Result<GroupOperationStatus, StudyGroupResponse>> UpdateGroupAsync(
        Guid groupId,
        UpdateStudyGroupRequest request,
        string? teacherUserId,
        CancellationToken cancellationToken)
    {
        var nextTeacherUserId = teacherUserId ?? request.TeacherUserId;
        var validationError = ValidateGroup(
            request.Name,
            nextTeacherUserId ?? string.Empty,
            request.StudentUserIds);
        if (validationError is not null || request.Revision < 0)
        {
            return new Result<GroupOperationStatus, StudyGroupResponse>(
                GroupOperationStatus.ValidationFailed,
                error: validationError ?? "Revision must be non-negative.");
        }

        var query = _dbContext.StudyGroups
            .AsSplitQuery()
            .Include(group => group.Students)
            .Include(group => group.Assignments)
                .ThenInclude(assignment => assignment.CourseTestVersion)
                    .ThenInclude(version => version.Tasks)
                        .ThenInclude(task => task.Options)
            .Include(group => group.Assignments)
                .ThenInclude(assignment => assignment.CourseTestVersion)
                    .ThenInclude(version => version.CourseTest)
            .Where(group => group.Id == groupId);
        if (!string.IsNullOrWhiteSpace(teacherUserId))
        {
            query = query.Where(group => group.TeacherUserId == teacherUserId);
        }

        var group = await query.SingleOrDefaultAsync(cancellationToken);
        if (group is null)
        {
            return new Result<GroupOperationStatus, StudyGroupResponse>(
                GroupOperationStatus.NotFound);
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
            ToResponse(group, includeHidden: true));
    }

    public async Task<List<TeachingService.Contracts.Models.CourseTestDto>?> GetAssignableTestsAsync(
        Guid groupId,
        string teacherUserId,
        CancellationToken cancellationToken)
    {
        var ownerUserId = await _dbContext.StudyGroups
            .AsNoTracking()
            .Where(group => group.Id == groupId && group.TeacherUserId == teacherUserId)
            .Select(group => group.TeacherUserId)
            .SingleOrDefaultAsync(cancellationToken);
        if (ownerUserId is null)
        {
            return null;
        }

        var tests = await _dbContext.Tests
            .AsNoTracking()
            .Where(test => test.TeacherUserId == ownerUserId)
            .Include(test => test.Versions)
            .ToListAsync(cancellationToken);
        var publishedVersions = tests
            .SelectMany(test => test.Versions
                .Where(version => version.PublishedAt.HasValue)
                .Select(version => new { Test = test, Version = version }))
            .ToList();
        var versionIds = publishedVersions.Select(item => item.Version.Id).ToArray();
        var versionsById = await _dbContext.TestVersions
            .AsNoTracking()
            .Where(version => versionIds.Contains(version.Id))
            .Include(version => version.Tasks)
                .ThenInclude(task => task.Options)
            .ToDictionaryAsync(version => version.Id, cancellationToken);

        return publishedVersions
            .Where(item => versionsById.ContainsKey(item.Version.Id))
            .Select(item => CourseTestMapper.ToDto(
                item.Test,
                versionsById[item.Version.Id],
                includeHidden: true))
            .OrderBy(test => test.Title)
            .ThenByDescending(test => test.VersionNumber)
            .ToList();
    }

    public async Task<Result<GroupOperationStatus, TestAssignmentResponse>> CreateAssignmentAsync(
        Guid groupId,
        CreateTestAssignmentRequest request,
        string teacherUserId,
        CancellationToken cancellationToken)
    {
        if (request.TestId == Guid.Empty || request.TestRevision <= 0)
        {
            return InvalidAssignment("A published test version is required.");
        }

        if (request.Deadline <= request.AvailableFrom)
        {
            return InvalidAssignment("Deadline must be later than AvailableFrom.");
        }

        if (request.Deadline <= DateTimeOffset.UtcNow)
        {
            return InvalidAssignment("Deadline must be in the future.");
        }

        var group = await _dbContext.StudyGroups
            .SingleOrDefaultAsync(group => group.Id == groupId
                && group.TeacherUserId == teacherUserId,
                cancellationToken);
        if (group is null)
        {
            return new Result<GroupOperationStatus, TestAssignmentResponse>(
                GroupOperationStatus.NotFound);
        }

        var test = await _dbContext.Tests
            .Include(courseTest => courseTest.Versions)
            .SingleOrDefaultAsync(courseTest => courseTest.Id == request.TestId
                && courseTest.TeacherUserId == group.TeacherUserId,
                cancellationToken);
        var version = test?.Versions.SingleOrDefault(item =>
            item.VersionNumber == request.TestRevision && item.PublishedAt.HasValue);
        if (test is null || version is null)
        {
            return InvalidAssignment("The selected published test version was not found.");
        }

        var assignment = new TestAssignment
        {
            Id = Guid.NewGuid(),
            StudyGroupId = group.Id,
            CourseTestVersionId = version.Id,
            AvailableFrom = request.AvailableFrom,
            Deadline = request.Deadline,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _dbContext.TestAssignments.Add(assignment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var stored = await AssignmentQuery(includeHidden: true)
            .SingleAsync(item => item.Id == assignment.Id, cancellationToken);
        return new Result<GroupOperationStatus, TestAssignmentResponse>(
            GroupOperationStatus.Success,
            ToResponse(stored, includeHidden: true));
    }

    public async Task<List<TestAssignmentResponse>> GetStudentAssignmentsAsync(
        string studentUserId,
        CancellationToken cancellationToken)
    {
        var assignments = await AssignmentQuery(includeHidden: false)
            .Where(assignment => assignment.StudyGroup.Students.Any(student =>
                student.StudentUserId == studentUserId))
            .ToListAsync(cancellationToken);

        return assignments
            .OrderBy(assignment => assignment.Deadline)
            .ThenBy(assignment => assignment.Id)
            .Select(assignment => ToResponse(assignment, includeHidden: false))
            .ToList();
    }

    public async Task<TestAssignmentResponse?> GetStudentAssignmentAsync(
        Guid assignmentId,
        string studentUserId,
        CancellationToken cancellationToken)
    {
        var assignment = await AssignmentQuery(includeHidden: false)
            .SingleOrDefaultAsync(item => item.Id == assignmentId
                && item.StudyGroup.Students.Any(student => student.StudentUserId == studentUserId),
                cancellationToken);
        return assignment is null ? null : ToResponse(assignment, includeHidden: false);
    }

    public async Task<TestAssignmentResponse?> GetAssignmentAsync(
        Guid assignmentId,
        bool includeHidden,
        CancellationToken cancellationToken)
    {
        var assignment = await AssignmentQuery(includeHidden)
            .SingleOrDefaultAsync(item => item.Id == assignmentId, cancellationToken);
        return assignment is null ? null : ToResponse(assignment, includeHidden);
    }

    private IQueryable<StudyGroup> GroupQuery()
    {
        return _dbContext.StudyGroups
            .AsNoTracking()
            .AsSplitQuery()
            .Include(group => group.Students)
            .Include(group => group.Assignments)
                .ThenInclude(assignment => assignment.CourseTestVersion)
                    .ThenInclude(version => version.Tasks)
                        .ThenInclude(task => task.Options)
            .Include(group => group.Assignments)
                .ThenInclude(assignment => assignment.CourseTestVersion)
                    .ThenInclude(version => version.CourseTest);
    }

    private IQueryable<TestAssignment> AssignmentQuery(bool includeHidden)
    {
        var query = _dbContext.TestAssignments
            .AsNoTracking()
            .AsSplitQuery()
            .Include(assignment => assignment.StudyGroup)
                .ThenInclude(group => group.Students)
            .Include(assignment => assignment.CourseTestVersion)
                .ThenInclude(version => version.CourseTest);

        return includeHidden
            ? query.Include(assignment => assignment.CourseTestVersion)
                .ThenInclude(version => version.Tasks)
                    .ThenInclude(task => task.Options)
            : query.Include(assignment => assignment.CourseTestVersion)
                .ThenInclude(version => version.Tasks.Where(task => !task.IsHidden))
                    .ThenInclude(task => task.Options);
    }

    private static StudyGroupResponse ToResponse(StudyGroup group, bool includeHidden)
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
            Assignments = group.Assignments
                .OrderBy(assignment => assignment.Deadline)
                .Select(assignment => ToResponse(assignment, includeHidden))
                .ToList(),
            CreatedAt = group.CreatedAt,
            UpdatedAt = group.UpdatedAt,
            Revision = group.Revision
        };
    }

    private static TestAssignmentResponse ToResponse(
        TestAssignment assignment,
        bool includeHidden)
    {
        var version = assignment.CourseTestVersion;
        return new TestAssignmentResponse
        {
            Id = assignment.Id,
            GroupId = assignment.StudyGroupId,
            GroupName = assignment.StudyGroup.Name,
            TeacherUserId = version.CourseTest.TeacherUserId,
            AvailableFrom = assignment.AvailableFrom,
            Deadline = assignment.Deadline,
            CreatedAt = assignment.CreatedAt,
            Test = CourseTestMapper.ToDto(version.CourseTest, version, includeHidden)
        };
    }

    private static string? ValidateGroup(
        string name,
        string teacherUserId,
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

        return studentUserIds.Any(studentUserId => studentUserId.Trim().Length > MaxUserIdLength)
            ? $"StudentUserIds must not exceed {MaxUserIdLength} characters."
            : null;
    }

    private static List<string> NormalizeStudentIds(IEnumerable<string> studentUserIds)
    {
        return studentUserIds
            .Select(studentUserId => studentUserId.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(studentUserId => studentUserId, StringComparer.Ordinal)
            .ToList();
    }

    private static Result<GroupOperationStatus, TestAssignmentResponse> InvalidAssignment(
        string error)
    {
        return new Result<GroupOperationStatus, TestAssignmentResponse>(
            GroupOperationStatus.ValidationFailed,
            error: error);
    }
}

using Microsoft.EntityFrameworkCore;
using TeachingService.Contracts.Models;
using TeachingService.Contracts.Requests;
using TeachingService.Contracts.Responses;
using TeachingService.Data;
using TeachingService.Enums;
using TeachingService.Interfaces;
using TeachingService.Mappers;
using TeachingService.Models;

namespace TeachingService.Services;

public class TestAssignmentService
{
    private readonly TeachingDbContext _dbContext;
    private readonly IGroupServiceClient _groupServiceClient;

    public TestAssignmentService(TeachingDbContext dbContext, IGroupServiceClient groupServiceClient)
    {
        _dbContext = dbContext;
        _groupServiceClient = groupServiceClient;
    }

    public async Task<List<CourseTestDto>?> GetAssignableTestsAsync(
        Guid groupId,
        string teacherUserId,
        CancellationToken cancellationToken)
    {
        var group = await GetAuthorizedGroupAsync(groupId, teacherUserId, cancellationToken);
        if (group is null)
        {
            return null;
        }

        var tests = await _dbContext.Tests
            .AsNoTracking()
            .Where(test => test.TeacherUserId == teacherUserId)
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
            .Select(item => CourseTestMapper.ToDto(item.Test, versionsById[item.Version.Id], includeHidden: true))
            .OrderBy(test => test.Title)
            .ThenByDescending(test => test.VersionNumber)
            .ToList();
    }

    public async Task<Result<AssignmentOperationStatus, TestAssignmentResponse>> CreateAssignmentAsync(
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

        var group = await GetAuthorizedGroupAsync(groupId, teacherUserId, cancellationToken);
        if (group is null)
        {
            return new Result<AssignmentOperationStatus, TestAssignmentResponse>(AssignmentOperationStatus.NotFound);
        }

        var test = await _dbContext.Tests
            .Include(courseTest => courseTest.Versions)
            .SingleOrDefaultAsync(courseTest => courseTest.Id == request.TestId
                && courseTest.TeacherUserId == teacherUserId, cancellationToken);
        var version = test?.Versions.SingleOrDefault(item =>
            item.VersionNumber == request.TestRevision && item.PublishedAt.HasValue);
        if (version is null)
        {
            return InvalidAssignment("The selected published test version was not found.");
        }

        var assignment = new TestAssignment
        {
            Id = Guid.NewGuid(),
            GroupId = group.Id,
            GroupName = group.Name,
            CourseTestVersionId = version.Id,
            AvailableFrom = request.AvailableFrom,
            Deadline = request.Deadline,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _dbContext.TestAssignments.Add(assignment);
        await _dbContext.SaveChangesAsync(cancellationToken);
        var stored = await AssignmentQuery(includeHidden: true)
            .SingleAsync(item => item.Id == assignment.Id, cancellationToken);
        return new Result<AssignmentOperationStatus, TestAssignmentResponse>(
            AssignmentOperationStatus.Success, ToResponse(stored, includeHidden: true));
    }

    public async Task<List<TestAssignmentResponse>?> GetGroupAssignmentsAsync(
        Guid groupId,
        string? teacherUserId,
        CancellationToken cancellationToken)
    {
        var group = await GetAuthorizedGroupAsync(groupId, teacherUserId, cancellationToken);
        if (group is null)
        {
            return null;
        }

        var assignments = await AssignmentQuery(includeHidden: true)
            .Where(assignment => assignment.GroupId == groupId)
            .ToListAsync(cancellationToken);
        return OrderAssignments(assignments)
            .Select(assignment => ToSummaryResponse(assignment, group.Name))
            .ToList();
    }

    public async Task<List<TestAssignmentResponse>> GetTeacherAssignmentsAsync(
        string teacherUserId,
        CancellationToken cancellationToken)
    {
        var assignments = await AssignmentQuery(includeHidden: true)
            .Where(assignment => assignment.CourseTestVersion.CourseTest.TeacherUserId == teacherUserId)
            .ToListAsync(cancellationToken);
        return OrderAssignments(assignments)
            .Select(assignment => ToResponse(assignment, includeHidden: true))
            .ToList();
    }

    public async Task<List<TestAssignmentResponse>> GetStudentAssignmentsAsync(
        string studentUserId,
        CancellationToken cancellationToken)
    {
        var groups = await _groupServiceClient.GetStudentGroupsAsync(studentUserId, cancellationToken);
        var groupsById = groups.ToDictionary(group => group.Id);
        var groupIds = groupsById.Keys.ToArray();
        if (groupIds.Length == 0)
        {
            return [];
        }

        var assignments = await AssignmentQuery(includeHidden: false)
            .Where(assignment => groupIds.Contains(assignment.GroupId))
            .ToListAsync(cancellationToken);
        return OrderAssignments(assignments)
            .Select(assignment => ToResponse(assignment, includeHidden: false, groupsById[assignment.GroupId].Name))
            .ToList();
    }

    public async Task<TestAssignmentResponse?> GetStudentAssignmentAsync(
        Guid assignmentId,
        string studentUserId,
        CancellationToken cancellationToken)
    {
        var assignment = await AssignmentQuery(includeHidden: false)
            .SingleOrDefaultAsync(item => item.Id == assignmentId, cancellationToken);
        if (assignment is null)
        {
            return null;
        }

        var group = await _groupServiceClient.GetGroupAsync(assignment.GroupId, cancellationToken);
        return group is null || !group.StudentUserIds.Contains(studentUserId, StringComparer.Ordinal)
            ? null
            : ToResponse(assignment, includeHidden: false, group.Name);
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

    private async Task<GroupService.Contracts.Responses.StudyGroupResponse?> GetAuthorizedGroupAsync(
        Guid groupId,
        string? teacherUserId,
        CancellationToken cancellationToken)
    {
        var group = await _groupServiceClient.GetGroupAsync(groupId, cancellationToken);
        return group is null || (teacherUserId is not null
            && !string.Equals(group.TeacherUserId, teacherUserId, StringComparison.Ordinal))
            ? null
            : group;
    }

    private IQueryable<TestAssignment> AssignmentQuery(bool includeHidden)
    {
        var query = _dbContext.TestAssignments
            .AsNoTracking()
            .AsSplitQuery()
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

    private static IOrderedEnumerable<TestAssignment> OrderAssignments(IEnumerable<TestAssignment> assignments)
    {
        return assignments.OrderBy(assignment => assignment.Deadline).ThenBy(assignment => assignment.Id);
    }

    private static TestAssignmentResponse ToSummaryResponse(TestAssignment assignment, string groupName)
    {
        var version = assignment.CourseTestVersion;
        return new TestAssignmentResponse
        {
            Id = assignment.Id,
            GroupId = assignment.GroupId,
            GroupName = groupName,
            TeacherUserId = version.CourseTest.TeacherUserId,
            AvailableFrom = assignment.AvailableFrom,
            Deadline = assignment.Deadline,
            CreatedAt = assignment.CreatedAt,
            Test = new CourseTestDto
            {
                Id = version.CourseTest.Id.ToString(),
                TeacherUserId = version.CourseTest.TeacherUserId,
                Title = version.Title,
                Subject = version.Subject,
                Status = version.Status,
                TimeLimitMinutes = version.TimeLimitMinutes,
                VersionNumber = version.VersionNumber,
                TotalPoints = version.Tasks.Where(task => !task.IsHidden).Sum(task => task.MaxPoints)
            }
        };
    }

    private static TestAssignmentResponse ToResponse(TestAssignment assignment, bool includeHidden, string? groupName = null)
    {
        var version = assignment.CourseTestVersion;
        return new TestAssignmentResponse
        {
            Id = assignment.Id,
            GroupId = assignment.GroupId,
            GroupName = groupName ?? assignment.GroupName,
            TeacherUserId = version.CourseTest.TeacherUserId,
            AvailableFrom = assignment.AvailableFrom,
            Deadline = assignment.Deadline,
            CreatedAt = assignment.CreatedAt,
            Test = CourseTestMapper.ToDto(version.CourseTest, version, includeHidden)
        };
    }

    private static Result<AssignmentOperationStatus, TestAssignmentResponse> InvalidAssignment(string error)
    {
        return new Result<AssignmentOperationStatus, TestAssignmentResponse>(
            AssignmentOperationStatus.ValidationFailed, error: error);
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using GroupService.Contracts.Requests;
using GroupService.Contracts.Responses;
using GroupService.Controllers;
using GroupService.Data;
using GroupService.Enums;
using GroupService.Services;

namespace GroupService.Tests;

public class StudyGroupServiceTests
{
    [Fact]
    public async Task CreateGroup_NormalizesNameTeacherAndMembership()
    {
        await using var dbContext = CreateDbContext();
        var service = new StudyGroupService(dbContext);
        var beforeCreation = DateTimeOffset.UtcNow;

        var result = await service.CreateGroupAsync(
            new CreateStudyGroupRequest
            {
                Name = " Group 1 ",
                StudentUserIds = [" student-2 ", "student-1", "student-2"]
            },
            " teacher-1 ",
            CancellationToken.None);

        Assert.Equal(GroupOperationStatus.Success, result.Status);
        var group = Assert.IsType<StudyGroupResponse>(result.Value);
        Assert.NotEqual(Guid.Empty, group.Id);
        Assert.Equal("Group 1", group.Name);
        Assert.Equal("teacher-1", group.TeacherUserId);
        Assert.Equal(["student-1", "student-2"], group.StudentUserIds);
        Assert.Equal(0, group.Revision);
        Assert.InRange(group.CreatedAt, beforeCreation, DateTimeOffset.UtcNow);
        Assert.Equal(group.CreatedAt, group.UpdatedAt);
        Assert.Equal(group.Id, Assert.Single(await dbContext.StudyGroups.ToListAsync()).Id);
        var memberships = await dbContext.StudyGroupStudents.OrderBy(student => student.StudentUserId).ToListAsync();
        Assert.Equal(["student-1", "student-2"], memberships.Select(student => student.StudentUserId));
        Assert.All(memberships, student =>
        {
            Assert.Equal(group.Id, student.StudyGroupId);
            Assert.Equal(group.CreatedAt, student.AddedAt);
        });
    }

    [Fact]
    public async Task AdminCreateGroup_UsesSelectedTeacher()
    {
        await using var dbContext = CreateDbContext();
        var controller = CreateController(new StudyGroupService(dbContext), "admin-1", "Admin");

        var result = await controller.CreateGroup(
            new CreateStudyGroupRequest
            {
                Name = "Admin group",
                TeacherUserId = "teacher-2",
                StudentUserIds = ["student-1"]
            },
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var group = Assert.IsType<StudyGroupResponse>(created.Value);
        Assert.Equal(nameof(StudyGroupsController.GetGroup), created.ActionName);
        Assert.Equal(group.Id, created.RouteValues!["groupId"]);
        Assert.Equal("teacher-2", group.TeacherUserId);
        Assert.Equal(["student-1"], group.StudentUserIds);
    }

    [Fact]
    public async Task TeacherCreateGroup_UsesCurrentTeacherInsteadOfRequestedTeacher()
    {
        await using var dbContext = CreateDbContext();
        var controller = CreateController(new StudyGroupService(dbContext), "teacher-1", "Teacher");

        var result = await controller.CreateGroup(
            new CreateStudyGroupRequest
            {
                Name = "Teacher group",
                TeacherUserId = "teacher-2",
                StudentUserIds = ["student-1"]
            },
            CancellationToken.None);

        var group = Assert.IsType<StudyGroupResponse>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        Assert.Equal("teacher-1", group.TeacherUserId);
        Assert.Equal("teacher-1", Assert.Single(await dbContext.StudyGroups.ToListAsync()).TeacherUserId);
    }

    [Fact]
    public async Task GetGroups_IsolatesTeacherGroupsAndAllowsAdminToListAll()
    {
        await using var dbContext = CreateDbContext();
        var service = new StudyGroupService(dbContext);
        var firstGroup = await CreateGroupAsync(service, "teacher-1", ["student-1"], "B group");
        var secondGroup = await CreateGroupAsync(service, "teacher-2", ["student-2"], "A group");
        var teacherController = CreateController(service, "teacher-1", "Teacher");
        var adminController = CreateController(service, "admin-1", "Admin");

        var teacherResult = await teacherController.GetGroups(CancellationToken.None);
        var adminResult = await adminController.GetGroups(CancellationToken.None);

        var teacherGroups = Assert.IsType<List<StudyGroupResponse>>(Assert.IsType<OkObjectResult>(teacherResult.Result).Value);
        Assert.Equal(firstGroup.Id, Assert.Single(teacherGroups).Id);
        var adminGroups = Assert.IsType<List<StudyGroupResponse>>(Assert.IsType<OkObjectResult>(adminResult.Result).Value);
        Assert.Equal([secondGroup.Id, firstGroup.Id], adminGroups.Select(group => group.Id));
    }

    [Fact]
    public async Task TeacherCannotReadOrUpdateAnotherTeachersGroup()
    {
        await using var dbContext = CreateDbContext();
        var service = new StudyGroupService(dbContext);
        var group = await CreateGroupAsync(service, "teacher-1", ["student-1"]);
        var controller = CreateController(service, "teacher-2", "Teacher");

        var readResult = await controller.GetGroup(group.Id, CancellationToken.None);
        var updateResult = await controller.UpdateGroup(
            group.Id,
            new UpdateStudyGroupRequest
            {
                Name = "Changed group",
                StudentUserIds = ["student-2"],
                Revision = group.Revision
            },
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(readResult.Result);
        Assert.IsType<NotFoundResult>(updateResult.Result);
        var stored = await service.GetGroupAsync(group.Id, teacherUserId: null, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(group.Name, stored.Name);
        Assert.Equal("teacher-1", stored.TeacherUserId);
        Assert.Equal(["student-1"], stored.StudentUserIds);
        Assert.Equal(group.Revision, stored.Revision);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task EmptyTeacherScope_DoesNotExpandToAdminScope(string teacherUserId)
    {
        await using var dbContext = CreateDbContext();
        var service = new StudyGroupService(dbContext);
        var group = await CreateGroupAsync(service, "teacher-1", ["student-1"]);

        Assert.Empty(await service.GetGroupsAsync(teacherUserId, CancellationToken.None));
        Assert.Null(await service.GetGroupAsync(group.Id, teacherUserId, CancellationToken.None));
    }

    [Fact]
    public async Task AdminUpdateGroup_ChangesTeacherAndMembershipAndTransfersTeacherScope()
    {
        await using var dbContext = CreateDbContext();
        var service = new StudyGroupService(dbContext);
        var group = await CreateGroupAsync(service, "teacher-1", ["student-1"]);
        var controller = CreateController(service, "admin-1", "Admin");

        var result = await controller.UpdateGroup(
            group.Id,
            new UpdateStudyGroupRequest
            {
                Name = "Transferred group",
                TeacherUserId = " teacher-2 ",
                StudentUserIds = ["student-3", "student-2"],
                Revision = group.Revision
            },
            CancellationToken.None);

        var updated = Assert.IsType<StudyGroupResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(group.Id, updated.Id);
        Assert.Equal("Transferred group", updated.Name);
        Assert.Equal("teacher-2", updated.TeacherUserId);
        Assert.Equal(["student-2", "student-3"], updated.StudentUserIds);
        Assert.Equal(group.CreatedAt, updated.CreatedAt);
        Assert.True(updated.UpdatedAt >= group.UpdatedAt);
        Assert.Equal(group.Revision + 1, updated.Revision);
        Assert.Empty(await service.GetGroupsAsync("teacher-1", CancellationToken.None));
        Assert.Null(await service.GetGroupAsync(group.Id, "teacher-1", CancellationToken.None));
        Assert.Equal(group.Id, Assert.Single(await service.GetGroupsAsync("teacher-2", CancellationToken.None)).Id);
        Assert.Empty(await service.GetStudentGroupsAsync("student-1", CancellationToken.None));
        Assert.Equal(group.Id, Assert.Single(await service.GetStudentGroupsAsync("student-2", CancellationToken.None)).Id);

        var previousTeacherUpdate = await service.UpdateGroupAsync(
            group.Id,
            new UpdateStudyGroupRequest
            {
                Name = "Previous teacher update",
                StudentUserIds = ["student-1"],
                Revision = updated.Revision
            },
            "teacher-1",
            CancellationToken.None);
        var currentTeacherUpdate = await service.UpdateGroupAsync(
            group.Id,
            new UpdateStudyGroupRequest
            {
                Name = "Current teacher update",
                StudentUserIds = ["student-2", "student-3"],
                Revision = updated.Revision
            },
            "teacher-2",
            CancellationToken.None);

        Assert.Equal(GroupOperationStatus.NotFound, previousTeacherUpdate.Status);
        Assert.Equal(GroupOperationStatus.Success, currentTeacherUpdate.Status);
        Assert.Equal("Current teacher update", currentTeacherUpdate.Value!.Name);
        Assert.Equal("teacher-2", currentTeacherUpdate.Value.TeacherUserId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task AdminUpdateGroup_RequiresTeacherAndDoesNotChangeGroup(string? teacherUserId)
    {
        await using var dbContext = CreateDbContext();
        var service = new StudyGroupService(dbContext);
        var group = await CreateGroupAsync(service, "teacher-1", ["student-1"]);
        var controller = CreateController(service, "admin-1", "Admin");

        var result = await controller.UpdateGroup(
            group.Id,
            new UpdateStudyGroupRequest
            {
                Name = "Changed group",
                TeacherUserId = teacherUserId,
                StudentUserIds = ["student-2"],
                Revision = group.Revision
            },
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal("TeacherUserId is required.", problem.Detail);
        var stored = await service.GetGroupAsync(group.Id, teacherUserId: null, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(group.Name, stored.Name);
        Assert.Equal("teacher-1", stored.TeacherUserId);
        Assert.Equal(["student-1"], stored.StudentUserIds);
        Assert.Equal(group.Revision, stored.Revision);
    }

    [Fact]
    public async Task TeacherUpdateGroup_DoesNotChangeTeacherAndPreservesRetainedMembership()
    {
        await using var dbContext = CreateDbContext();
        var service = new StudyGroupService(dbContext);
        var group = await CreateGroupAsync(service, "teacher-1", ["student-1", "student-2"]);
        var retainedAddedAt = (await dbContext.StudyGroupStudents.SingleAsync(
            student => student.StudentUserId == "student-2")).AddedAt;
        var controller = CreateController(service, "teacher-1", "Teacher");

        var result = await controller.UpdateGroup(
            group.Id,
            new UpdateStudyGroupRequest
            {
                Name = "Updated group",
                TeacherUserId = "teacher-2",
                StudentUserIds = ["student-2", "student-3", " student-3 "],
                Revision = group.Revision
            },
            CancellationToken.None);

        var updated = Assert.IsType<StudyGroupResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal("Updated group", updated.Name);
        Assert.Equal("teacher-1", updated.TeacherUserId);
        Assert.Equal(["student-2", "student-3"], updated.StudentUserIds);
        Assert.Equal(retainedAddedAt, (await dbContext.StudyGroupStudents.SingleAsync(
            student => student.StudentUserId == "student-2")).AddedAt);
        Assert.Equal(["student-2", "student-3"], await dbContext.StudyGroupStudents
            .OrderBy(student => student.StudentUserId)
            .Select(student => student.StudentUserId)
            .ToListAsync());
    }

    [Fact]
    public async Task StudentGroups_FollowCurrentMembershipAfterAddAndRemoval()
    {
        await using var dbContext = CreateDbContext();
        var service = new StudyGroupService(dbContext);
        var group = await CreateGroupAsync(service, "teacher-1", ["student-1", "student-2"]);
        var anotherGroup = await CreateGroupAsync(service, "teacher-2", ["student-1"], "Group 2");
        var controller = new InternalGroupsController(service);

        var before = await controller.GetStudentGroups("student-1", CancellationToken.None);
        var previousGroups = Assert.IsType<List<StudyGroupResponse>>(Assert.IsType<OkObjectResult>(before.Result).Value);
        Assert.Equal([group.Id, anotherGroup.Id], previousGroups.Select(item => item.Id));
        Assert.Empty(await service.GetStudentGroupsAsync("student-3", CancellationToken.None));

        var update = await service.UpdateGroupAsync(
            group.Id,
            new UpdateStudyGroupRequest
            {
                Name = group.Name,
                StudentUserIds = ["student-2", "student-3"],
                Revision = group.Revision
            },
            "teacher-1",
            CancellationToken.None);

        Assert.Equal(GroupOperationStatus.Success, update.Status);
        var afterRemoval = await controller.GetStudentGroups("student-1", CancellationToken.None);
        var remainingGroups = Assert.IsType<List<StudyGroupResponse>>(
            Assert.IsType<OkObjectResult>(afterRemoval.Result).Value);
        Assert.Equal(anotherGroup.Id, Assert.Single(remainingGroups).Id);
        var afterAddition = await controller.GetStudentGroups("student-3", CancellationToken.None);
        var addedGroups = Assert.IsType<List<StudyGroupResponse>>(Assert.IsType<OkObjectResult>(afterAddition.Result).Value);
        var addedGroup = Assert.Single(addedGroups);
        Assert.Equal(group.Id, addedGroup.Id);
        Assert.Equal(["student-2", "student-3"], addedGroup.StudentUserIds);
        Assert.Equal(update.Value!.Revision, addedGroup.Revision);
        Assert.Equal(group.Id, Assert.Single(await service.GetStudentGroupsAsync("student-2", CancellationToken.None)).Id);
    }

    [Fact]
    public async Task UpdateGroup_RejectsStaleRevisionWithoutChangingPersistedData()
    {
        await using var dbContext = CreateDbContext();
        var service = new StudyGroupService(dbContext);
        var group = await CreateGroupAsync(service, "teacher-1", ["student-1"]);
        var firstUpdate = await service.UpdateGroupAsync(
            group.Id,
            new UpdateStudyGroupRequest
            {
                Name = "Updated group",
                StudentUserIds = ["student-2"],
                Revision = group.Revision
            },
            "teacher-1",
            CancellationToken.None);
        var controller = CreateController(service, "admin-1", "Admin");

        var staleUpdate = await controller.UpdateGroup(
            group.Id,
            new UpdateStudyGroupRequest
            {
                Name = "Stale group",
                TeacherUserId = "teacher-2",
                StudentUserIds = ["student-3"],
                Revision = group.Revision
            },
            CancellationToken.None);

        Assert.Equal(GroupOperationStatus.Success, firstUpdate.Status);
        var conflict = Assert.IsType<ConflictObjectResult>(staleUpdate.Result);
        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ProblemDetails>(conflict.Value).Status);
        var stored = await service.GetGroupAsync(group.Id, teacherUserId: null, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal("Updated group", stored.Name);
        Assert.Equal("teacher-1", stored.TeacherUserId);
        Assert.Equal(["student-2"], stored.StudentUserIds);
        Assert.Equal(group.Revision + 1, stored.Revision);
        Assert.Equal(firstUpdate.Value!.UpdatedAt, stored.UpdatedAt);
    }

    [Fact]
    public async Task ConcurrentUpdate_ReturnsConflictAndRollsBackMembershipChanges()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<GroupDbContext>().UseSqlite(connection).Options;
        await using var firstContext = new GroupDbContext(options);
        await firstContext.Database.EnsureCreatedAsync();
        var firstService = new StudyGroupService(firstContext);
        var group = await CreateGroupAsync(firstService, "teacher-1", ["student-1"]);
        await using var staleContext = new GroupDbContext(options);
        await staleContext.StudyGroups.Include(item => item.Students).SingleAsync(item => item.Id == group.Id);
        var staleService = new StudyGroupService(staleContext);

        var firstUpdate = await firstService.UpdateGroupAsync(
            group.Id,
            new UpdateStudyGroupRequest
            {
                Name = "Winning update",
                StudentUserIds = ["student-2"],
                Revision = group.Revision
            },
            "teacher-1",
            CancellationToken.None);
        var staleUpdate = await staleService.UpdateGroupAsync(
            group.Id,
            new UpdateStudyGroupRequest
            {
                Name = "Losing update",
                StudentUserIds = ["student-3"],
                Revision = group.Revision
            },
            "teacher-1",
            CancellationToken.None);

        Assert.Equal(GroupOperationStatus.Success, firstUpdate.Status);
        Assert.Equal(GroupOperationStatus.Conflict, staleUpdate.Status);
        await using var verificationContext = new GroupDbContext(options);
        var stored = await new StudyGroupService(verificationContext).GetGroupAsync(
            group.Id, teacherUserId: null, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal("Winning update", stored.Name);
        Assert.Equal("teacher-1", stored.TeacherUserId);
        Assert.Equal(["student-2"], stored.StudentUserIds);
        Assert.Equal(group.Revision + 1, stored.Revision);
        Assert.Equal("student-2", Assert.Single(await verificationContext.StudyGroupStudents.ToListAsync()).StudentUserId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task CreateGroup_RejectsEmptyStudentIdWithoutPersistingGroup(string? studentUserId)
    {
        await using var dbContext = CreateDbContext();
        var service = new StudyGroupService(dbContext);

        var result = await service.CreateGroupAsync(
            new CreateStudyGroupRequest
            {
                Name = "Group 1",
                StudentUserIds = [studentUserId!]
            },
            "teacher-1",
            CancellationToken.None);

        Assert.Equal(GroupOperationStatus.ValidationFailed, result.Status);
        Assert.Equal("StudentUserIds must not contain empty values.", result.Error);
        Assert.Empty(await dbContext.StudyGroups.ToListAsync());
        Assert.Empty(await dbContext.StudyGroupStudents.ToListAsync());
    }

    [Fact]
    public async Task TeacherWithoutUserId_CannotReadCreateOrUpdateGroups()
    {
        await using var dbContext = CreateDbContext();
        var service = new StudyGroupService(dbContext);
        var group = await CreateGroupAsync(service, "teacher-1", ["student-1"]);
        var controller = CreateController(service, userId: null, "Teacher");

        var list = await controller.GetGroups(CancellationToken.None);
        var read = await controller.GetGroup(group.Id, CancellationToken.None);
        var create = await controller.CreateGroup(
            new CreateStudyGroupRequest
            {
                Name = "New group",
                TeacherUserId = "teacher-1",
                StudentUserIds = ["student-2"]
            },
            CancellationToken.None);
        var update = await controller.UpdateGroup(
            group.Id,
            new UpdateStudyGroupRequest
            {
                Name = "Changed group",
                StudentUserIds = ["student-2"],
                Revision = group.Revision
            },
            CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(list.Result);
        Assert.IsType<UnauthorizedResult>(read.Result);
        Assert.IsType<UnauthorizedResult>(create.Result);
        Assert.IsType<UnauthorizedResult>(update.Result);
        var stored = Assert.Single(await dbContext.StudyGroups.ToListAsync());
        Assert.Equal(group.Name, stored.Name);
        Assert.Equal(group.Revision, stored.Revision);
        Assert.Equal("student-1", Assert.Single(await dbContext.StudyGroupStudents.ToListAsync()).StudentUserId);
    }

    [Fact]
    public void InitialMigration_MatchesCurrentPostgresModel()
    {
        var options = new DbContextOptionsBuilder<GroupDbContext>()
            .UseNpgsql("Host=localhost;Database=llm_tutor_groups;Username=postgres;Password=postgres")
            .Options;
        using var dbContext = new GroupDbContext(options);

        Assert.Equal(["20261009090000_InitialCreate"], dbContext.Database.GetMigrations());
        Assert.False(dbContext.Database.HasPendingModelChanges());
    }

    private static GroupDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<GroupDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var dbContext = new GroupDbContext(options);
        dbContext.Database.OpenConnection();
        dbContext.Database.EnsureCreated();
        return dbContext;
    }

    private static async Task<StudyGroupResponse> CreateGroupAsync(
        StudyGroupService service,
        string teacherUserId,
        IList<string> studentUserIds,
        string name = "Group 1")
    {
        var result = await service.CreateGroupAsync(
            new CreateStudyGroupRequest
            {
                Name = name,
                StudentUserIds = studentUserIds
            },
            teacherUserId,
            CancellationToken.None);
        Assert.Equal(GroupOperationStatus.Success, result.Status);
        return Assert.IsType<StudyGroupResponse>(result.Value);
    }

    private static StudyGroupsController CreateController(
        StudyGroupService service,
        string? userId,
        params string[] roles)
    {
        var claims = roles.Select(role => new Claim(ClaimTypes.Role, role)).ToList();
        if (userId is not null)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
        }

        return new StudyGroupsController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuthentication"))
                }
            }
        };
    }
}

using AttemptService.Data;
using AttemptService.Interfaces;
using AttemptService.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TeachingService.Contracts.Responses;

namespace AttemptService.Tests;

public sealed class TestAttemptDbContextFactory : IDbContextFactory<AttemptDbContext>, IAsyncDisposable
{
    private readonly DbContextOptions<AttemptDbContext> _options;
    private readonly SqliteConnection? _connection;

    private TestAttemptDbContextFactory(
        DbContextOptions<AttemptDbContext> options,
        SqliteConnection? connection = null)
    {
        _options = options;
        _connection = connection;
    }

    public static TestAttemptDbContextFactory CreateInMemory()
    {
        var options = new DbContextOptionsBuilder<AttemptDbContext>()
            .UseInMemoryDatabase($"attempt-tests-{Guid.NewGuid():N}")
            .Options;
        return new TestAttemptDbContextFactory(options);
    }

    public static async Task<TestAttemptDbContextFactory> CreateSqliteAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AttemptDbContext>()
            .UseSqlite(connection)
            .ReplaceService<IModelCustomizer, SqliteAttemptModelCustomizer>()
            .Options;
        var factory = new TestAttemptDbContextFactory(options, connection);

        await using var dbContext = factory.CreateDbContext();
        await dbContext.Database.EnsureCreatedAsync();
        return factory;
    }

    public AttemptDbContext CreateDbContext()
    {
        return new AttemptDbContext(_options);
    }

    public Task<AttemptDbContext> CreateDbContextAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CreateDbContext());
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}

public class SqliteAttemptModelCustomizer : ModelCustomizer
{
    public SqliteAttemptModelCustomizer(ModelCustomizerDependencies dependencies)
        : base(dependencies)
    {
    }

    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);
        modelBuilder.Entity<AttemptSubmissionOutboxMessage>()
            .Property(message => message.PublishedAt)
            .HasConversion(new DateTimeOffsetToBinaryConverter());
    }
}

public sealed class StubTeachingServiceClient : ITeachingServiceClient
{
    public Dictionary<Guid, TestAssignmentResponse> Assignments { get; } = new();
    public Dictionary<Guid, HashSet<string>> AssignmentStudentIds { get; } = new();
    public HttpRequestException? RequestException { get; set; }
    public int RequestCount { get; private set; }
    public int ListRequestCount { get; private set; }
    public Guid? LastAssignmentId { get; private set; }
    public string? LastStudentUserId { get; private set; }

    public Task<List<TestAssignmentResponse>> GetStudentAssignmentsAsync(
        string studentUserId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ListRequestCount++;
        LastStudentUserId = studentUserId;
        if (RequestException is not null)
        {
            throw RequestException;
        }

        return Task.FromResult(Assignments.Values
            .Where(assignment => CanAccessAssignment(assignment.Id, studentUserId))
            .ToList());
    }

    public Task<TestAssignmentResponse?> GetStudentAssignmentAsync(
        Guid assignmentId,
        string studentUserId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequestCount++;
        LastAssignmentId = assignmentId;
        LastStudentUserId = studentUserId;
        if (RequestException is not null)
        {
            throw RequestException;
        }

        if (!CanAccessAssignment(assignmentId, studentUserId))
        {
            return Task.FromResult<TestAssignmentResponse?>(null);
        }

        Assignments.TryGetValue(assignmentId, out var assignment);
        return Task.FromResult(assignment);
    }

    private bool CanAccessAssignment(Guid assignmentId, string studentUserId)
    {
        return !AssignmentStudentIds.TryGetValue(assignmentId, out var studentIds)
            || studentIds.Contains(studentUserId);
    }
}

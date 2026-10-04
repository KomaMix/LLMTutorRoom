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
    public int RequestCount { get; private set; }
    public Guid? LastAssignmentId { get; private set; }
    public string? LastStudentUserId { get; private set; }

    public Task<TestAssignmentResponse?> GetStudentAssignmentAsync(
        Guid assignmentId,
        string studentUserId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequestCount++;
        LastAssignmentId = assignmentId;
        LastStudentUserId = studentUserId;
        Assignments.TryGetValue(assignmentId, out var assignment);
        return Task.FromResult(assignment);
    }
}

using AttemptService.Data;
using AttemptService.Interfaces;
using AttemptService.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TeachingService.Contracts.Models;

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

public sealed class SqliteAttemptModelCustomizer(
    ModelCustomizerDependencies dependencies) : ModelCustomizer(dependencies)
{
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
    public Dictionary<string, CourseTestDto> Tests { get; } = new();
    public int RequestCount { get; private set; }
    public bool? LastIncludeHidden { get; private set; }
    public int? LastVersionNumber { get; private set; }

    public Task<CourseTestDto?> GetTestAsync(
        Guid testId,
        bool includeHidden,
        int? versionNumber,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequestCount++;
        LastIncludeHidden = includeHidden;
        LastVersionNumber = versionNumber;
        Tests.TryGetValue(testId.ToString("D"), out var test);
        return Task.FromResult(test);
    }
}

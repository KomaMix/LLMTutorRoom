using Microsoft.EntityFrameworkCore;
using TeachingService.Data;

namespace TeachingService.Tests;

public sealed class GradingExampleJsonMappingTests
{
    [Fact]
    public void CatalogQuery_CompilesWithVersionGradingExamplesForPostgreSql()
    {
        using var dbContext = CreateDbContext();

        var sql = dbContext.Tests
            .AsNoTracking()
            .Include(test => test.Versions)
            .Where(test => test.TeacherUserId != string.Empty)
            .ToQueryString();

        Assert.Contains("\"GradingExamples\"", sql);
    }

    [Fact]
    public void VersionDetailsQuery_CompilesWithTaskGradingExamplesForPostgreSql()
    {
        using var dbContext = CreateDbContext();

        var sql = dbContext.TestVersions
            .AsNoTracking()
            .Include(version => version.Tasks)
            .ThenInclude(task => task.Options)
            .ToQueryString();

        Assert.Contains("\"GradingExamples\"", sql);
        Assert.Contains("\"TestTasks\"", sql);
    }

    [Fact]
    public void GradingExamples_KeepExistingJsonbColumns()
    {
        using var dbContext = CreateDbContext();
        var model = dbContext.Model.GetRelationalModel();

        Assert.Equal("jsonb", model.FindTable("TestVersions", null)!
            .FindColumn("GradingExamples")!.StoreType);
        Assert.Equal("jsonb", model.FindTable("TestTasks", null)!
            .FindColumn("GradingExamples")!.StoreType);
    }

    private static TeachingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TeachingDbContext>()
            .UseNpgsql("Host=localhost;Database=teaching_query_tests;Username=tests")
            .Options;

        return new TeachingDbContext(options);
    }
}

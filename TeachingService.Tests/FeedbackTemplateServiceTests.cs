using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TeachingService.Contracts.Enums;
using TeachingService.Contracts.Models;
using TeachingService.Contracts.Requests;
using TeachingService.Data;
using TeachingService.Enums;
using TeachingService.Services;

namespace TeachingService.Tests;

public class FeedbackTemplateServiceTests
{
    [Fact]
    public async Task SystemTemplates_AreAvailableAndImmutable()
    {
        await using var fixture = await CreateFixtureAsync();
        var templates = await fixture.Service.GetAllAsync("teacher", CancellationToken.None);

        Assert.Equal(4, templates.Count);
        Assert.All(templates, template => Assert.True(template.IsSystem));

        var systemTemplate = templates[0];
        var updateResult = await fixture.Service.UpdateAsync(
            systemTemplate.Id,
            ToRequest(systemTemplate),
            "teacher",
            CancellationToken.None);
        var deleteResult = await fixture.Service.DeleteAsync(
            systemTemplate.Id,
            "teacher",
            CancellationToken.None);

        Assert.Equal(CatalogOperationStatus.ValidationFailed, updateResult.Status);
        Assert.Equal(CatalogOperationStatus.ValidationFailed, deleteResult.Status);
    }

    [Fact]
    public async Task PersonalTemplate_IsNormalizedOwnedAndConcurrencyProtected()
    {
        await using var fixture = await CreateFixtureAsync();
        var request = new SaveFeedbackTemplateRequest
        {
            Name = "  Мой шаблон  ",
            Description = "  Для письменных работ  ",
            Tone = FeedbackTone.Supportive,
            DetailLevel = FeedbackDetailLevel.Detailed,
            AnswerDisclosure = FeedbackAnswerDisclosure.HintsOnly,
            CustomInstruction = "  Учитывай терминологию.  ",
            Sections =
            [
                new FeedbackTemplateSectionDto
                {
                    Title = "  Подсказка  ",
                    Instruction = "  Дай направление для исправления.  ",
                    Format = FeedbackSectionFormat.Text,
                    MaxItems = 5,
                    IsRequired = true
                }
            ]
        };

        var createResult = await fixture.Service.CreateAsync(
            request,
            "teacher-a",
            CancellationToken.None);
        var created = Assert.IsType<FeedbackTemplateDto>(createResult.Value);

        Assert.Equal(CatalogOperationStatus.Success, createResult.Status);
        Assert.Equal("Мой шаблон", created.Name);
        Assert.Equal("Для письменных работ", created.Description);
        Assert.Equal("Учитывай терминологию.", created.CustomInstruction);
        var section = Assert.Single(created.Sections);
        Assert.NotEqual(Guid.Empty, section.Id);
        Assert.Equal(1, section.MaxItems);
        Assert.Null(await fixture.Service.GetAvailableAsync(
            created.Id,
            "teacher-b",
            CancellationToken.None));

        var updateRequest = ToRequest(created);
        updateRequest.Name = "Обновленный шаблон";
        var updateResult = await fixture.Service.UpdateAsync(
            created.Id,
            updateRequest,
            "teacher-a",
            CancellationToken.None);
        var updated = Assert.IsType<FeedbackTemplateDto>(updateResult.Value);

        Assert.Equal(CatalogOperationStatus.Success, updateResult.Status);
        Assert.Equal(1, updated.Revision);
        Assert.Equal("Обновленный шаблон", updated.Name);

        var staleResult = await fixture.Service.UpdateAsync(
            created.Id,
            updateRequest,
            "teacher-a",
            CancellationToken.None);
        Assert.Equal(CatalogOperationStatus.Conflict, staleResult.Status);
    }

    private static SaveFeedbackTemplateRequest ToRequest(FeedbackTemplateDto template)
    {
        return new SaveFeedbackTemplateRequest
        {
            Name = template.Name,
            Description = template.Description,
            Tone = template.Tone,
            DetailLevel = template.DetailLevel,
            AnswerDisclosure = template.AnswerDisclosure,
            CustomInstruction = template.CustomInstruction,
            Sections = template.Sections.Select(section => new FeedbackTemplateSectionDto
            {
                Id = section.Id,
                Title = section.Title,
                Instruction = section.Instruction,
                Format = section.Format,
                MaxItems = section.MaxItems,
                IsRequired = section.IsRequired
            }).ToList(),
            Revision = template.Revision
        };
    }

    private static async Task<FeedbackTemplateFixture> CreateFixtureAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<TeachingDbContext>()
            .UseSqlite(connection)
            .Options;
        var dbContext = new TeachingDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        return new FeedbackTemplateFixture(
            connection,
            dbContext,
            new FeedbackTemplateService(dbContext));
    }

    private class FeedbackTemplateFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly TeachingDbContext _dbContext;

        public FeedbackTemplateFixture(
            SqliteConnection connection,
            TeachingDbContext dbContext,
            FeedbackTemplateService service)
        {
            _connection = connection;
            _dbContext = dbContext;
            Service = service;
        }

        public FeedbackTemplateService Service { get; }

        public async ValueTask DisposeAsync()
        {
            await _dbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}

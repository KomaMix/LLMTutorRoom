using Microsoft.EntityFrameworkCore;
using TeachingService.Contracts.Enums;
using TeachingService.Contracts.Models;
using TeachingService.Contracts.Requests;
using TeachingService.Data;
using TeachingService.Enums;
using TeachingService.Helpers;
using TeachingService.Models;

namespace TeachingService.Services;

public class FeedbackTemplateService
{
    private const int MaximumSections = 6;
    private readonly TeachingDbContext _dbContext;

    public FeedbackTemplateService(TeachingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<FeedbackTemplateDto>> GetAllAsync(
        string teacherUserId,
        CancellationToken cancellationToken)
    {
        EnsureTeacherUserId(teacherUserId);
        var personalTemplates = await _dbContext.FeedbackTemplates
            .AsNoTracking()
            .Where(template => template.TeacherUserId == teacherUserId)
            .OrderBy(template => template.Name)
            .ToListAsync(cancellationToken);

        return SystemFeedbackTemplates.CreateAll()
            .Concat(personalTemplates.Select(FeedbackTemplateJson.ToDto))
            .ToList();
    }

    public async Task<FeedbackTemplateDto?> GetAvailableAsync(
        Guid id,
        string teacherUserId,
        CancellationToken cancellationToken)
    {
        EnsureTeacherUserId(teacherUserId);
        var systemTemplate = SystemFeedbackTemplates.Find(id);
        if (systemTemplate is not null)
        {
            return systemTemplate;
        }

        var template = await _dbContext.FeedbackTemplates
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == id && item.TeacherUserId == teacherUserId,
                cancellationToken);
        return template is null ? null : FeedbackTemplateJson.ToDto(template);
    }

    public async Task<Result<CatalogOperationStatus, FeedbackTemplateDto>> CreateAsync(
        SaveFeedbackTemplateRequest request,
        string teacherUserId,
        CancellationToken cancellationToken)
    {
        EnsureTeacherUserId(teacherUserId);
        var validation = ValidateAndNormalize(request);
        if (validation.Error is not null)
        {
            return new(CatalogOperationStatus.ValidationFailed, error: validation.Error);
        }

        var now = DateTimeOffset.UtcNow;
        var template = new FeedbackTemplate
        {
            Id = Guid.NewGuid(),
            TeacherUserId = teacherUserId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            Tone = request.Tone,
            DetailLevel = request.DetailLevel,
            AnswerDisclosure = request.AnswerDisclosure,
            CustomInstruction = request.CustomInstruction?.Trim() ?? string.Empty,
            SectionsJson = FeedbackTemplateJson.SerializeSections(validation.Sections),
            Revision = 0,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.FeedbackTemplates.Add(template);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new(CatalogOperationStatus.Success, FeedbackTemplateJson.ToDto(template));
    }

    public async Task<Result<CatalogOperationStatus, FeedbackTemplateDto>> UpdateAsync(
        Guid id,
        SaveFeedbackTemplateRequest request,
        string teacherUserId,
        CancellationToken cancellationToken)
    {
        EnsureTeacherUserId(teacherUserId);
        if (SystemFeedbackTemplates.Find(id) is not null)
        {
            return new(
                CatalogOperationStatus.ValidationFailed,
                error: "Системный шаблон нельзя изменить. Создайте его копию.");
        }

        var validation = ValidateAndNormalize(request);
        if (validation.Error is not null)
        {
            return new(CatalogOperationStatus.ValidationFailed, error: validation.Error);
        }

        var template = await _dbContext.FeedbackTemplates.SingleOrDefaultAsync(
            item => item.Id == id && item.TeacherUserId == teacherUserId,
            cancellationToken);
        if (template is null)
        {
            return new(CatalogOperationStatus.NotFound);
        }

        if (template.Revision != request.Revision)
        {
            return new(
                CatalogOperationStatus.Conflict,
                error: "Шаблон уже изменен. Обновите страницу и повторите правку.");
        }

        template.Name = request.Name.Trim();
        template.Description = request.Description?.Trim() ?? string.Empty;
        template.Tone = request.Tone;
        template.DetailLevel = request.DetailLevel;
        template.AnswerDisclosure = request.AnswerDisclosure;
        template.CustomInstruction = request.CustomInstruction?.Trim() ?? string.Empty;
        template.SectionsJson = FeedbackTemplateJson.SerializeSections(validation.Sections);
        template.Revision = checked(template.Revision + 1);
        template.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(
                CatalogOperationStatus.Conflict,
                error: "Шаблон уже изменен. Обновите страницу и повторите правку.");
        }

        return new(CatalogOperationStatus.Success, FeedbackTemplateJson.ToDto(template));
    }

    public async Task<Result<CatalogOperationStatus, bool>> DeleteAsync(
        Guid id,
        string teacherUserId,
        CancellationToken cancellationToken)
    {
        EnsureTeacherUserId(teacherUserId);
        if (SystemFeedbackTemplates.Find(id) is not null)
        {
            return new(
                CatalogOperationStatus.ValidationFailed,
                error: "Системный шаблон нельзя удалить.");
        }

        var template = await _dbContext.FeedbackTemplates.SingleOrDefaultAsync(
            item => item.Id == id && item.TeacherUserId == teacherUserId,
            cancellationToken);
        if (template is null)
        {
            return new(CatalogOperationStatus.NotFound);
        }

        _dbContext.FeedbackTemplates.Remove(template);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new(CatalogOperationStatus.Success, true);
    }

    private static (string? Error, List<FeedbackTemplateSectionDto> Sections) ValidateAndNormalize(
        SaveFeedbackTemplateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 120)
        {
            return ("Название шаблона должно содержать от 1 до 120 символов.", []);
        }

        if ((request.Description?.Trim().Length ?? 0) > 500)
        {
            return ("Описание шаблона не должно превышать 500 символов.", []);
        }

        if ((request.CustomInstruction?.Trim().Length ?? 0) > 2000)
        {
            return ("Дополнительная инструкция не должна превышать 2000 символов.", []);
        }

        if (!Enum.IsDefined(request.Tone)
            || !Enum.IsDefined(request.DetailLevel)
            || !Enum.IsDefined(request.AnswerDisclosure))
        {
            return ("Выбраны неподдерживаемые настройки шаблона.", []);
        }

        if (request.Sections is null || request.Sections.Count is < 1 or > MaximumSections)
        {
            return ($"Добавьте от 1 до {MaximumSections} разделов обратной связи.", []);
        }

        var normalized = new List<FeedbackTemplateSectionDto>();
        var sectionIds = new HashSet<Guid>();
        foreach (var section in request.Sections)
        {
            if (section is null)
            {
                return ("Шаблон содержит пустой раздел.", []);
            }

            var id = section.Id == Guid.Empty ? Guid.NewGuid() : section.Id;
            if (!sectionIds.Add(id))
            {
                return ("Разделы шаблона должны иметь уникальные идентификаторы.", []);
            }

            if (string.IsNullOrWhiteSpace(section.Title) || section.Title.Trim().Length > 120)
            {
                return ("Название каждого раздела должно содержать от 1 до 120 символов.", []);
            }

            if (string.IsNullOrWhiteSpace(section.Instruction) || section.Instruction.Trim().Length > 1000)
            {
                return ("Инструкция каждого раздела должна содержать от 1 до 1000 символов.", []);
            }

            if (!Enum.IsDefined(section.Format))
            {
                return ("Выбран неподдерживаемый формат раздела.", []);
            }

            var maxItems = section.Format == FeedbackSectionFormat.List ? section.MaxItems : 1;
            if (maxItems is < 1 or > 5)
            {
                return ("Количество пунктов в списке должно быть от 1 до 5.", []);
            }

            normalized.Add(new FeedbackTemplateSectionDto
            {
                Id = id,
                Title = section.Title.Trim(),
                Instruction = section.Instruction.Trim(),
                Format = section.Format,
                MaxItems = maxItems,
                IsRequired = section.IsRequired
            });
        }

        return (null, normalized);
    }

    private static void EnsureTeacherUserId(string teacherUserId)
    {
        if (string.IsNullOrWhiteSpace(teacherUserId))
        {
            throw new ArgumentException("Teacher user id is required.", nameof(teacherUserId));
        }
    }
}

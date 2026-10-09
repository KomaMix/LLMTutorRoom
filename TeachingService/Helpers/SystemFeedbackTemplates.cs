using TeachingService.Contracts.Enums;
using TeachingService.Contracts.Models;

namespace TeachingService.Helpers;

public static class SystemFeedbackTemplates
{
    public static readonly Guid BalancedId = Guid.Parse("7a9a6d92-d8a5-4f7c-9bd4-6f7f52694401");
    public static readonly Guid ConciseId = Guid.Parse("7a9a6d92-d8a5-4f7c-9bd4-6f7f52694402");
    public static readonly Guid DetailedId = Guid.Parse("7a9a6d92-d8a5-4f7c-9bd4-6f7f52694403");
    public static readonly Guid HintsId = Guid.Parse("7a9a6d92-d8a5-4f7c-9bd4-6f7f52694404");

    public static FeedbackTemplateDto GetDefault()
    {
        return CreateAll().Single(template => template.Id == BalancedId);
    }

    public static FeedbackTemplateDto? Find(Guid id)
    {
        return CreateAll().SingleOrDefault(template => template.Id == id);
    }

    public static List<FeedbackTemplateDto> CreateAll()
    {
        return
        [
            CreateTemplate(
                BalancedId,
                "Сбалансированная обратная связь",
                "Краткий разбор сильных сторон, ошибок и следующего шага без готового ответа.",
                FeedbackTone.Supportive,
                FeedbackDetailLevel.Standard,
                FeedbackAnswerDisclosure.ExplainApproach,
                [
                    CreateSection("755f5c88-018e-44ed-9535-49af05215e01", "Что получилось", "Отметь конкретные сильные стороны ответа.", FeedbackSectionFormat.List, 3),
                    CreateSection("755f5c88-018e-44ed-9535-49af05215e02", "Что улучшить", "Назови главные ошибки или пробелы и объясни их без готового полного ответа.", FeedbackSectionFormat.List, 3),
                    CreateSection("755f5c88-018e-44ed-9535-49af05215e03", "Следующий шаг", "Дай один понятный следующий шаг для доработки ответа.", FeedbackSectionFormat.Text, 1)
                ]),
            CreateTemplate(
                ConciseId,
                "Краткий комментарий",
                "Минимальная обратная связь для частых проверок.",
                FeedbackTone.Neutral,
                FeedbackDetailLevel.Concise,
                FeedbackAnswerDisclosure.ExplainApproach,
                [
                    CreateSection("755f5c88-018e-44ed-9535-49af05215e11", "Итог", "Кратко объясни выставленный балл.", FeedbackSectionFormat.Text, 1),
                    CreateSection("755f5c88-018e-44ed-9535-49af05215e12", "Главное улучшение", "Укажи самое важное изменение ответа.", FeedbackSectionFormat.Text, 1)
                ]),
            CreateTemplate(
                DetailedId,
                "Подробный разбор",
                "Развернутая проверка с объяснением ошибок, рекомендациями и примером ответа.",
                FeedbackTone.Formal,
                FeedbackDetailLevel.Detailed,
                FeedbackAnswerDisclosure.MayShowAnswer,
                [
                    CreateSection("755f5c88-018e-44ed-9535-49af05215e21", "Оценка ответа", "Объясни соответствие ответа требованиям задания.", FeedbackSectionFormat.Text, 1),
                    CreateSection("755f5c88-018e-44ed-9535-49af05215e22", "Сильные стороны", "Перечисли удачные элементы ответа.", FeedbackSectionFormat.List, 4),
                    CreateSection("755f5c88-018e-44ed-9535-49af05215e23", "Ошибки и пробелы", "Перечисли ошибки, неточности и недостающие элементы.", FeedbackSectionFormat.List, 5),
                    CreateSection("755f5c88-018e-44ed-9535-49af05215e24", "Как исправить", "Дай последовательные рекомендации по исправлению.", FeedbackSectionFormat.List, 4),
                    CreateSection("755f5c88-018e-44ed-9535-49af05215e25", "Возможный ответ", "Покажи корректный вариант или фрагмент ответа, если это помогает обучению.", FeedbackSectionFormat.Text, 1, false)
                ]),
            CreateTemplate(
                HintsId,
                "Только подсказки",
                "Направляет ученика к исправлению, не раскрывая решение.",
                FeedbackTone.Supportive,
                FeedbackDetailLevel.Standard,
                FeedbackAnswerDisclosure.HintsOnly,
                [
                    CreateSection("755f5c88-018e-44ed-9535-49af05215e31", "Направление", "Объясни, в какой части рассуждения нужно искать проблему, не раскрывая ответ.", FeedbackSectionFormat.Text, 1),
                    CreateSection("755f5c88-018e-44ed-9535-49af05215e32", "Подсказки", "Дай наводящие подсказки от общей к более конкретной.", FeedbackSectionFormat.List, 3),
                    CreateSection("755f5c88-018e-44ed-9535-49af05215e33", "Вопросы для самопроверки", "Сформулируй вопросы, которые помогут ученику самостоятельно исправить ответ.", FeedbackSectionFormat.List, 3)
                ])
        ];
    }

    private static FeedbackTemplateDto CreateTemplate(
        Guid id,
        string name,
        string description,
        FeedbackTone tone,
        FeedbackDetailLevel detailLevel,
        FeedbackAnswerDisclosure answerDisclosure,
        List<FeedbackTemplateSectionDto> sections)
    {
        return new FeedbackTemplateDto
        {
            Id = id,
            Name = name,
            Description = description,
            IsSystem = true,
            Tone = tone,
            DetailLevel = detailLevel,
            AnswerDisclosure = answerDisclosure,
            Sections = sections,
            Revision = 1,
            CreatedAt = DateTimeOffset.UnixEpoch,
            UpdatedAt = DateTimeOffset.UnixEpoch
        };
    }

    private static FeedbackTemplateSectionDto CreateSection(
        string id,
        string title,
        string instruction,
        FeedbackSectionFormat format,
        int maxItems,
        bool isRequired = true)
    {
        return new FeedbackTemplateSectionDto
        {
            Id = Guid.Parse(id),
            Title = title,
            Instruction = instruction,
            Format = format,
            MaxItems = maxItems,
            IsRequired = isRequired
        };
    }
}

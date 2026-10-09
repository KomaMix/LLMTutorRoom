using System.Text.Json;
using System.Text.Json.Serialization;
using TeachingService.Contracts.Models;
using TeachingService.Models;

namespace TeachingService.Helpers;

public static class FeedbackTemplateJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    public static string SerializeSections(IEnumerable<FeedbackTemplateSectionDto> sections)
    {
        return JsonSerializer.Serialize(sections, Options);
    }

    public static List<FeedbackTemplateSectionDto> DeserializeSections(string json)
    {
        return JsonSerializer.Deserialize<List<FeedbackTemplateSectionDto>>(json, Options) ?? [];
    }

    public static string SerializeSnapshot(FeedbackTemplateDto template)
    {
        return JsonSerializer.Serialize(template, Options);
    }

    public static FeedbackTemplateDto? DeserializeSnapshot(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<FeedbackTemplateDto>(json, Options);
    }

    public static FeedbackTemplateDto ToDto(FeedbackTemplate template)
    {
        return new FeedbackTemplateDto
        {
            Id = template.Id,
            Name = template.Name,
            Description = template.Description,
            IsSystem = false,
            Tone = template.Tone,
            DetailLevel = template.DetailLevel,
            AnswerDisclosure = template.AnswerDisclosure,
            CustomInstruction = template.CustomInstruction,
            Sections = DeserializeSections(template.SectionsJson),
            Revision = template.Revision,
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt
        };
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReviewService.Helpers;

public static class JsonHelper
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };
}

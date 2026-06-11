namespace VideoAnalytics.Infrastructure.Kafka;

using System.Text.Json;
using System.Text.Json.Serialization;

internal static class PipelineEventJsonOptions
{
    public static readonly JsonSerializerOptions Instance = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) }
    };
}

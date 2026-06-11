namespace VideoAnalytics.Infrastructure.Kafka;

using System.Text.Json;

internal sealed record DlqMessage(
    JsonElement OriginalEvent,
    string Error,
    int RetryCount,
    string ConsumerGroup,
    DateTimeOffset FailedAt,
    int Partition,
    long Offset);

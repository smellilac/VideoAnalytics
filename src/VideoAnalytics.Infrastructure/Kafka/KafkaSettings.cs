namespace VideoAnalytics.Infrastructure.Kafka;

using Confluent.Kafka;

internal sealed class KafkaSettings
{
    public string BootstrapServers { get; init; } = "localhost:9092";
    public string StatusChangedTopic { get; init; } = "dataset.status.changes";
    public string DatasetReadyTopic { get; init; } = "dataset.ready";

    // Confluent.Kafka built-in retry — no Polly retry on top of these
    public int MessageSendMaxRetries { get; init; } = 3;
    public int RetryBackoffMs { get; init; } = 100;

    // Upper bound for all retries combined; keeps ProduceAsync from blocking indefinitely
    public int MessageTimeoutMs { get; init; } = 5000;

    // Circuit breaker: opens after failures reach MinimumThroughput within SamplingDuration
    public int CircuitBreakerMinimumThroughput { get; init; } = 3;
    public int CircuitBreakerSamplingDurationSeconds { get; init; } = 60;
    public int CircuitBreakerBreakDurationSeconds { get; init; } = 30;

    public CompressionType CompressionType { get; init; } = CompressionType.Snappy;
    public int BatchSize { get; init; } = 16384;
    public int LingerMs { get; init; } = 15;
}

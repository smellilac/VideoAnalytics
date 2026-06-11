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

    // --- Consumer: topics ---
    public string PipelineEventsTopic { get; init; } = "pipeline.dataset.events";
    public string PipelineEventsDlqTopic { get; init; } = "pipeline.dataset.events.dlq";

    // --- Consumer: group & offset ---
    public string ConsumerGroupId { get; init; } = "videoanalytics-pipeline-consumer";
    public AutoOffsetReset AutoOffsetReset { get; init; } = AutoOffsetReset.Earliest;

    // --- Consumer: group protocol timeouts (defaults — do not tune without a reason) ---
    public int SessionTimeoutMs { get; init; } = 45000;
    public int HeartbeatIntervalMs { get; init; } = 3000;
    public int MaxPollIntervalMs { get; init; } = 300000;

    // --- Consumer: per-message processing retry (NOT Confluent/Polly retry — see design doc 4.2) ---
    public int ConsumerMaxRetries { get; init; } = 3;
    public int ConsumerRetryBaseDelayMs { get; init; } = 1000; // 1s, 2s, 4s (2^attempt)

    // --- Optional security (filled via K8s Secret in prod, empty in dev) ---
    public string? SaslUsername { get; init; }
    public string? SaslPassword { get; init; }
    public string? SaslMechanism { get; init; }
    public string? SecurityProtocol { get; init; }
}

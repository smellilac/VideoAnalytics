namespace VideoAnalytics.Infrastructure.Kafka;

using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using VideoAnalytics.Application.Datasets.Common;
using VideoAnalytics.Application.Interfaces;

internal sealed class KafkaEventPublisher : IEventPublisher, IAsyncDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly string _statusChangedTopic;
    private readonly string _datasetReadyTopic;
    private readonly ILogger<KafkaEventPublisher> _logger;
    private readonly ResiliencePipeline _pipeline;

    public KafkaEventPublisher(IOptions<KafkaSettings> options, ILogger<KafkaEventPublisher> logger)
    {
        var settings = options.Value;
        _statusChangedTopic = settings.StatusChangedTopic;
        _datasetReadyTopic = settings.DatasetReadyTopic;
        _logger = logger;

        _producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = settings.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageSendMaxRetries = settings.MessageSendMaxRetries,
            RetryBackoffMs = settings.RetryBackoffMs,
            MessageTimeoutMs = settings.MessageTimeoutMs,
            CompressionType = settings.CompressionType,
            BatchSize = settings.BatchSize,
            LingerMs = settings.LingerMs,
        }).Build();

        _pipeline = new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                // Only KafkaException (and its subtype ProduceException<,>) trip the breaker
                ShouldHandle = new PredicateBuilder().Handle<KafkaException>(),
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(settings.CircuitBreakerSamplingDurationSeconds),
                MinimumThroughput = settings.CircuitBreakerMinimumThroughput,
                BreakDuration = TimeSpan.FromSeconds(settings.CircuitBreakerBreakDurationSeconds),
                OnOpened = args =>
                {
                    _logger.LogWarning(
                        "Kafka circuit breaker opened for {Duration}s — publish calls will fast-fail until Kafka recovers",
                        args.BreakDuration.TotalSeconds);
                    return ValueTask.CompletedTask;
                },
                OnClosed = _ =>
                {
                    _logger.LogInformation("Kafka circuit breaker closed — publish calls will resume");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = _ =>
                {
                    _logger.LogInformation("Kafka circuit breaker half-open — testing if Kafka has recovered");
                    return ValueTask.CompletedTask;
                },
            })
            .Build();
    }

    public Task PublishStatusChangedAsync(
        Guid datasetId, string fromStatus, string toStatus, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new StatusChangedPayload(datasetId, fromStatus, toStatus));
        return PublishAsync(_statusChangedTopic, datasetId.ToString(), payload, cancellationToken);
    }

    public Task PublishDatasetReadyAsync(Guid datasetId, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new DatasetReadyPayload(datasetId));
        return PublishAsync(_datasetReadyTopic, datasetId.ToString(), payload, cancellationToken);
    }

    private async Task PublishAsync(
        string topic, string key, string value, CancellationToken cancellationToken)
    {
        await _pipeline.ExecuteAsync(async ct =>
        {
            var result = await _producer.ProduceAsync(
                topic,
                new Message<string, string> { Key = key, Value = value },
                ct);

            if (result.Status != PersistenceStatus.Persisted)
            {
                _logger.LogError(
                    "Message not persisted — Topic: {Topic}, Key: {Key}, Status: {Status}, PayloadSize: {Size}",
                    topic, key, result.Status, value.Length);

                // Throw so the outbox marks the message for retry and the circuit breaker counts the failure
                throw new ProduceException<string, string>(
                    new Error(ErrorCode.Local_MsgTimedOut, $"Message not persisted: {result.Status}"),
                    result);
            }

            _logger.LogDebug(
                "Published message — Topic: {Topic}, Partition: {Partition}, Offset: {Offset}",
                topic, result.Partition.Value, result.Offset.Value);
        }, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        // Flush ensures in-flight messages are delivered before the producer is torn down
        await Task.Run(() => _producer.Flush(TimeSpan.FromSeconds(5)));
        _producer.Dispose();
    }
}


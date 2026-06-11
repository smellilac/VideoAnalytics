namespace VideoAnalytics.Infrastructure.Kafka;

using System.Text.Json;
using Confluent.Kafka;
using ErrorOr;
using Error = ErrorOr.Error;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VideoAnalytics.Application.Datasets.AddDependency;
using VideoAnalytics.Application.Datasets.Common;
using VideoAnalytics.Application.Datasets.RegisterArtifact;
using VideoAnalytics.Application.Datasets.RegisterDataset;
using VideoAnalytics.Application.Datasets.UpdateStatus;
using VideoAnalytics.Domain.Datasets;

internal sealed class PipelineEventConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaSettings> options,
    TimeProvider timeProvider,
    ILogger<PipelineEventConsumer> logger) : BackgroundService, IAsyncDisposable
{
    private readonly KafkaSettings _settings = options.Value;
    private readonly IConsumer<string, string> _consumer = BuildConsumer(options.Value);
    private readonly IProducer<string, string> _dlqProducer = BuildDlqProducer(options.Value);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe(_settings.PipelineEventsTopic);

        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string> consumeResult;
            try
            {
                consumeResult = _consumer.Consume(stoppingToken);
            }
            catch (ConsumeException ex)
            {
                logger.LogError(ex,
                    "PIPELINE_CONSUME_ERROR code={Code} reason={Reason} is_fatal={IsFatal}",
                    ex.Error.Code, ex.Error.Reason, ex.Error.IsFatal);

                if (ex.Error.IsFatal)
                {
                    // Unrecoverable for this client instance — let the BackgroundService stop.
                    // Kubernetes restarts the pod, consumer rejoins the group from last committed offset.
                    throw;
                }

                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken); // avoid tight error-log loop
                continue;
            }

            await ProcessMessageAsync(consumeResult, stoppingToken);
            _consumer.Commit(consumeResult);
        }
    }

    private async Task ProcessMessageAsync(
        ConsumeResult<string, string> consumeResult,
        CancellationToken ct)
    {
        var raw = consumeResult.Message.Value;

        // Step 1: parse envelope
        PipelineEventEnvelope envelope;
        
        try
        {
            envelope = JsonSerializer.Deserialize<PipelineEventEnvelope>(raw, PipelineEventJsonOptions.Instance)
                ?? throw new JsonException("null envelope");
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "PIPELINE_EVENT_REJECTED reason=broken_json raw={Raw}", raw);
            return; // skip + commit
        }

        // Step 2: discriminator check
        if (!PipelineEventTypes.All.Contains(envelope.EventType))
        {
            logger.LogWarning(
                "PIPELINE_EVENT_REJECTED reason=unknown_event_type event_type={EventType} event_id={EventId}",
                envelope.EventType, envelope.EventId);
            return; // skip + commit
        }

        // Step 3: dispatch with retry
        for (var attempt = 0; attempt < _settings.ConsumerMaxRetries; attempt++)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                var result = await DispatchAsync(envelope, mediator, ct);

                if (result.IsError)
                {
                    HandleBusinessError(result.FirstError, envelope);
                    return; // skip + commit — business errors do not retry
                }

                return; // success → commit
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (attempt == _settings.ConsumerMaxRetries - 1)
                {
                    await WriteToDlqAsync(consumeResult, envelope, ex, attempt + 1, ct);
                    return; // DLQ written → commit
                }

                var delay = TimeSpan.FromMilliseconds(_settings.ConsumerRetryBaseDelayMs * Math.Pow(2, attempt));
                logger.LogWarning(
                    ex,
                    "PIPELINE_EVENT_RETRY attempt={Attempt} event_id={EventId} delay_ms={DelayMs}",
                    attempt + 1, envelope.EventId, delay.TotalMilliseconds);
                await Task.Delay(delay, ct);
            }
        }
    }

    private static async Task<ErrorOr<Success>> DispatchAsync(
        PipelineEventEnvelope envelope,
        IMediator mediator,
        CancellationToken ct)
    {
        switch (envelope.EventType)
        {
            case PipelineEventTypes.DatasetRegistered:
            {
                var data = envelope.Data.Deserialize<DatasetRegisteredData>(PipelineEventJsonOptions.Instance)!;
                var cmd = new RegisterDatasetCommand(data.Name, data.Version, data.PipelineRunId, data.Metadata);
                return ToResult(await mediator.Send(cmd, ct));
            }

            case PipelineEventTypes.StatusChanged:
            {
                var data = envelope.Data.Deserialize<DatasetStatusUpdateData>(PipelineEventJsonOptions.Instance)!;
                var cmd = new UpdateDatasetStatusCommand(data.DatasetId, data.NewStatus, data.Message, data.Metadata);
                return await mediator.Send(cmd, ct);
            }

            case PipelineEventTypes.ArtifactRegistered:
            {
                var data = envelope.Data.Deserialize<DatasetArtifactRegisteredData>(PipelineEventJsonOptions.Instance)!;
                var cmd = new RegisterArtifactCommand(data.DatasetId, data.S3Key, data.ArtifactType, data.SizeBytes, data.RowCount);
                return ToResult(await mediator.Send(cmd, ct));
            }

            case PipelineEventTypes.DependencyAdded:
            {
                var data = envelope.Data.Deserialize<DatasetDependencyAddedData>(PipelineEventJsonOptions.Instance)!;
                var cmd = new AddDependencyCommand(data.DatasetId, data.DependsOnDatasetId);
                return await mediator.Send(cmd, ct);
            }

            default:
                // Unreachable — EventType already validated against PipelineEventTypes.All
                throw new InvalidOperationException($"Unhandled event type '{envelope.EventType}'.");
        }
    }

    private void HandleBusinessError(Error error, PipelineEventEnvelope envelope)
    {
        switch (error.Type)
        {
            case ErrorType.NotFound:
                logger.LogWarning(
                    "PIPELINE_EVENT_REJECTED reason=not_found event_type={EventType} event_id={EventId} detail={Detail}",
                    envelope.EventType, envelope.EventId, error.Description);
                break;

            case ErrorType.Validation:
                logger.LogWarning(
                    "PIPELINE_EVENT_REJECTED reason=invalid_transition event_type={EventType} event_id={EventId} detail={Detail}",
                    envelope.EventType, envelope.EventId, error.Description);
                break;

            case ErrorType.Conflict:
                // LogError, not Warning — signals an idempotency bug in the handler
                logger.LogError(
                    "PIPELINE_EVENT_REJECTED reason=unexpected_conflict event_type={EventType} event_id={EventId} detail={Detail}",
                    envelope.EventType, envelope.EventId, error.Description);
                break;

            default:
                logger.LogError(
                    "PIPELINE_EVENT_REJECTED reason=unhandled_error_type error_type={ErrorType} event_type={EventType} event_id={EventId} detail={Detail}",
                    error.Type, envelope.EventType, envelope.EventId, error.Description);
                break;
        }
    }

    private async Task WriteToDlqAsync(
        ConsumeResult<string, string> consumeResult,
        PipelineEventEnvelope envelope,
        Exception exception,
        int retryCount,
        CancellationToken ct)
    {
        var originalEvent = JsonSerializer.SerializeToElement(envelope, PipelineEventJsonOptions.Instance);
        var dlqMessage = new DlqMessage(
            OriginalEvent: originalEvent,
            Error: exception.ToString(),
            RetryCount: retryCount,
            ConsumerGroup: _settings.ConsumerGroupId,
            FailedAt: timeProvider.GetUtcNow(),
            Partition: consumeResult.Partition.Value,
            Offset: consumeResult.Offset.Value);

        var payload = JsonSerializer.Serialize(dlqMessage, PipelineEventJsonOptions.Instance);

        try
        {
            await _dlqProducer.ProduceAsync(
                _settings.PipelineEventsDlqTopic,
                new Message<string, string> { Key = envelope.EventId.ToString(), Value = payload },
                ct);

            logger.LogError(
                exception,
                "PIPELINE_EVENT_DLQ event_type={EventType} event_id={EventId} retry_count={RetryCount}",
                envelope.EventType, envelope.EventId, retryCount);
        }
        catch (Exception dlqEx)
        {
            // Do not propagate — caller commits the offset regardless to avoid partition blocking
            logger.LogCritical(
                dlqEx,
                "PIPELINE_EVENT_DLQ_WRITE_FAILED event_id={EventId} — original_error={OriginalError}",
                envelope.EventId, exception.Message);
        }
    }

    private static ErrorOr<Success> ToResult<T>(ErrorOr<T> result) =>
        result.IsError ? result.Errors : Result.Success;

    private static IConsumer<string, string> BuildConsumer(KafkaSettings settings)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = settings.BootstrapServers,
            GroupId = settings.ConsumerGroupId,
            AutoOffsetReset = settings.AutoOffsetReset,
            EnableAutoCommit = false,
            SessionTimeoutMs = settings.SessionTimeoutMs,
            HeartbeatIntervalMs = settings.HeartbeatIntervalMs,
            MaxPollIntervalMs = settings.MaxPollIntervalMs,
        };

        if (!string.IsNullOrEmpty(settings.SaslUsername))
        {
            config.SaslUsername = settings.SaslUsername;
            config.SaslPassword = settings.SaslPassword;

            if (Enum.TryParse<SaslMechanism>(settings.SaslMechanism, ignoreCase: true, out var mechanism))
                config.SaslMechanism = mechanism;

            if (Enum.TryParse<SecurityProtocol>(settings.SecurityProtocol, ignoreCase: true, out var protocol))
                config.SecurityProtocol = protocol;
        }

        return new ConsumerBuilder<string, string>(config).Build();
    }

    private static IProducer<string, string> BuildDlqProducer(KafkaSettings settings)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = settings.BootstrapServers,
            Acks = Acks.Leader,
        };

        if (!string.IsNullOrEmpty(settings.SaslUsername))
        {
            config.SaslUsername = settings.SaslUsername;
            config.SaslPassword = settings.SaslPassword;

            if (Enum.TryParse<SaslMechanism>(settings.SaslMechanism, ignoreCase: true, out var mechanism))
                config.SaslMechanism = mechanism;

            if (Enum.TryParse<SecurityProtocol>(settings.SecurityProtocol, ignoreCase: true, out var protocol))
                config.SecurityProtocol = protocol;
        }

        return new ProducerBuilder<string, string>(config).Build();
    }

    public async ValueTask DisposeAsync()
    {
        _consumer.Close();
        _consumer.Dispose();
        await Task.Run(() => _dlqProducer.Flush(TimeSpan.FromSeconds(5)));
        _dlqProducer.Dispose();
    }
}

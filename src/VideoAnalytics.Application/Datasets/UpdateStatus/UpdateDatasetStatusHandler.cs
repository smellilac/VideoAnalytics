using VideoAnalytics.Application.Datasets.Common;

namespace VideoAnalytics.Application.Datasets.UpdateStatus;

using System.Text.Json;
using ErrorOr;
using Mediator;
using Microsoft.Extensions.Logging;
using VideoAnalytics.Application.Common;
using VideoAnalytics.Application.Interfaces;
using VideoAnalytics.Domain.Datasets;
using VideoAnalytics.Domain.Outbox;

public sealed class UpdateDatasetStatusHandler(
    IDatasetRepository repository,
    IEventPublisher eventPublisher,
    ICacheService cacheService,
    IArtifactStorage artifactStorage,
    TimeProvider timeProvider,
    ILogger<UpdateDatasetStatusHandler> logger)
    : ICommandHandler<UpdateDatasetStatusCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(UpdateDatasetStatusCommand command, CancellationToken cancellationToken)
    {
        var dataset = await repository.GetByIdAsync(command.DatasetId, cancellationToken);
        if (dataset is null)
            return DatasetErrors.NotFound(command.DatasetId);

        // Idempotency for Kafka redelivery — see design doc section 3.2.
        // Not the same as the FSM rule "Ready has no outgoing transitions" (DatasetStatusTransitions) —
        // this is "the command was already applied in a previous delivery".
        if (dataset.Status == command.NewStatus)
            return new Success();

        var fromStatus = dataset.Status;

        if (command.NewStatus == DatasetStatus.Ready)
        {
            var readinessError = await ValidateReadinessAsync(dataset, cancellationToken);
            if (readinessError is not null)
                return readinessError.Value;
        }

        var now = timeProvider.GetUtcNow();

        try
        {
            dataset.TransitionTo(command.NewStatus, now, command.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Error.Validation("Dataset.InvalidTransition", ex.Message);
        }

        dataset.MergeMetadata(command.Metadata); 
        
        var history = DatasetStatusHistory.Create(
            dataset.Id,
            fromStatus,
            command.NewStatus,
            now,
            command.Message);

        var outboxPayload = JsonSerializer.Serialize(
            new StatusChangedPayload(dataset.Id, fromStatus.ToString(), command.NewStatus.ToString()));
        
        var outboxMessage = OutboxMessage.Create(OutboxMessageTypes.DatasetStatusChanged, outboxPayload, now);

        await repository.SaveTransitionAsync(dataset, history, outboxMessage, cancellationToken);

        logger.LogInformation(
            "Dataset {DatasetId} transitioned from {FromStatus} to {ToStatus}",
            dataset.Id, fromStatus, command.NewStatus);

        if (command.NewStatus == DatasetStatus.Ready)
        {
            await eventPublisher.PublishDatasetReadyAsync(dataset.Id, cancellationToken);

            // Invalidate report:summary:{dataset_id} which could be cached
            // report:engagement:* and report:trends:* invalidates by TTL.
            await cacheService.InvalidateAsync(dataset.Id, cancellationToken);
        }

        return new Success();
    }

    private async Task<Error?> ValidateReadinessAsync(Dataset dataset, CancellationToken cancellationToken)
    {
        var readiness = await repository.CheckReadinessAsync(dataset.Id, cancellationToken);
        if (!readiness.IsReady)
            return DatasetErrors.DependenciesNotReady(readiness.Reason);

        var artifacts = await repository.GetArtifactsAsync(dataset.Id, cancellationToken);

        var existsChecks = await Task.WhenAll(
            artifacts.Select(async a => (a.S3Key, Exists: await artifactStorage.ExistsAsync(a.S3Key, cancellationToken))));

        var missingKeys = existsChecks
            .Where(c => !c.Exists)
            .Select(c => c.S3Key)
            .ToList();

        if (missingKeys.Count > 0)
            return DatasetErrors.ArtifactMissing(missingKeys);

        return null;
    }
}

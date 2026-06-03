namespace VideoAnalytics.Application.Datasets.ResetDataset;

using System.Text.Json;
using ErrorOr;
using Mediator;
using Microsoft.Extensions.Logging;
using VideoAnalytics.Application.Datasets.Common;
using VideoAnalytics.Application.Interfaces;
using VideoAnalytics.Domain.Datasets;
using VideoAnalytics.Domain.Outbox;

public sealed class ResetDatasetHandler(
    IDatasetRepository repository,
    TimeProvider timeProvider,
    ILogger<ResetDatasetHandler> logger)
    : ICommandHandler<ResetDatasetCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(
        ResetDatasetCommand command,
        CancellationToken cancellationToken)
    {
        var dataset = await repository.GetByIdAsync(command.DatasetId, cancellationToken);
        if (dataset is null)
            return DatasetErrors.NotFound(command.DatasetId);

        var fromStatus = dataset.Status;
        var now = timeProvider.GetUtcNow();

        try
        {
            dataset.TransitionTo(DatasetStatus.Pending, now);
        }
        catch (InvalidOperationException ex)
        {
            return Error.Validation("Dataset.InvalidTransition", ex.Message);
        }

        var history = DatasetStatusHistory.Create(
            dataset.Id,
            fromStatus,
            DatasetStatus.Pending,
            now);

        var outboxPayload = JsonSerializer.Serialize(
            new StatusChangedPayload(dataset.Id, fromStatus.ToString(), DatasetStatus.Pending.ToString()));
        var outboxMessage = OutboxMessage.Create(OutboxMessageTypes.DatasetStatusChanged, outboxPayload, now);

        await repository.SaveTransitionAsync(dataset, history, outboxMessage, cancellationToken);

        logger.LogInformation(
            "Dataset {DatasetId} reset from {FromStatus} to Pending",
            dataset.Id, fromStatus);

        return new Success();
    }
}

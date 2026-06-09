namespace VideoAnalytics.Application.Datasets.RegisterDataset;

using ErrorOr;
using Mediator;
using Microsoft.Extensions.Logging;
using VideoAnalytics.Application.Interfaces;
using VideoAnalytics.Domain.Datasets;
using VideoAnalytics.Application.Datasets.Common;

public sealed class RegisterDatasetHandler(
    IDatasetRepository repository,
    TimeProvider timeProvider,
    ILogger<RegisterDatasetHandler> logger)
    : ICommandHandler<RegisterDatasetCommand, ErrorOr<RegisterDatasetResponse>>
{
    public async ValueTask<ErrorOr<RegisterDatasetResponse>> Handle(
        RegisterDatasetCommand command,
        CancellationToken cancellationToken)
    {
        if (await repository.ExistsAsync(command.Name, command.Version, cancellationToken))
            return DatasetErrors.AlreadyExists(command.Name, command.Version);

        var dataset = Dataset.Create(
            command.Name,
            command.Version,
            command.PipelineRunId,
            command.Metadata,
            timeProvider);
        
        try
        {
            await repository.AddAsync(dataset, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // Defensive layer: concurrent request registered the same (Name, Version) between ExistsAsync and AddAsync
            return DatasetErrors.AlreadyExists(command.Name, command.Version);
        }

        logger.LogInformation(
            "Dataset {DatasetId} registered with name {Name} version {Version}",
            dataset.Id, dataset.Name, dataset.Version);

        return new RegisterDatasetResponse(
            dataset.Id,
            dataset.Name,
            dataset.Version,
            dataset.Status.ToString(),
            dataset.CreatedAt);
    }
}

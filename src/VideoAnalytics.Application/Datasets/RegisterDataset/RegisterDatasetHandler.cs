namespace VideoAnalytics.Application.Datasets.RegisterDataset;

using Mediator;
using Microsoft.Extensions.Logging;
using VideoAnalytics.Application.Common;
using VideoAnalytics.Application.Interfaces;
using VideoAnalytics.Domain.Datasets;

public sealed class RegisterDatasetHandler(
    IDatasetRepository repository,
    TimeProvider timeProvider,
    ILogger<RegisterDatasetHandler> logger)
    : ICommandHandler<RegisterDatasetCommand, RegisterDatasetResponse>
{
    public async ValueTask<RegisterDatasetResponse> Handle(
        RegisterDatasetCommand command,
        CancellationToken cancellationToken)
    {
        if (await repository.ExistsAsync(command.Name, command.Version, cancellationToken))
            throw new ConflictException(
                $"Dataset '{command.Name}' version '{command.Version}' already exists.");

        var dataset = Dataset.Create(
            command.Name,
            command.Version,
            command.PipelineRunId,
            command.Metadata,
            timeProvider);

        await repository.AddAsync(dataset, cancellationToken);

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

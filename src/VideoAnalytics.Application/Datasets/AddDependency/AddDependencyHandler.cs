namespace VideoAnalytics.Application.Datasets.AddDependency;

using ErrorOr;
using Mediator;
using Microsoft.Extensions.Logging;
using VideoAnalytics.Application.Datasets.Common;
using VideoAnalytics.Application.Interfaces;
using VideoAnalytics.Domain.Datasets;

public sealed class AddDependencyHandler(
    IDatasetRepository repository,
    ILogger<AddDependencyHandler> logger)
    : ICommandHandler<AddDependencyCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(
        AddDependencyCommand command,
        CancellationToken cancellationToken)
    {
        var dataset = await repository.GetByIdAsync(command.DatasetId, cancellationToken);
        if (dataset is null)
            return DatasetErrors.NotFound(command.DatasetId);

        var target = await repository.GetByIdAsync(command.DependsOnDatasetId, cancellationToken);
        if (target is null)
            return DatasetErrors.DependencyTargetNotFound(command.DependsOnDatasetId);

        if (await repository.DependencyExistsAsync(command.DatasetId, command.DependsOnDatasetId, cancellationToken))
            return new Success();

        if (await repository.WouldCreateCycleAsync(command.DatasetId, command.DependsOnDatasetId, cancellationToken))
            return DatasetErrors.CircularDependency(command.DatasetId, command.DependsOnDatasetId);

        var dependency = new DatasetDependency(command.DatasetId, command.DependsOnDatasetId);
        await repository.AddDependencyAsync(dependency, cancellationToken);

        logger.LogInformation(
            "Dependency added: dataset {DatasetId} now depends on {DependsOnDatasetId}",
            command.DatasetId, command.DependsOnDatasetId);

        return new Success();
    }
}

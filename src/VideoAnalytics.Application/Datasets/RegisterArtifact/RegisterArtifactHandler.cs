using VideoAnalytics.Application.Datasets.Common;

namespace VideoAnalytics.Application.Datasets.RegisterArtifact;

using ErrorOr;
using Mediator;
using Microsoft.Extensions.Logging;
using VideoAnalytics.Application.Interfaces;
using VideoAnalytics.Domain.Datasets;

public sealed class RegisterArtifactHandler(
    IDatasetRepository repository,
    TimeProvider timeProvider,
    ILogger<RegisterArtifactHandler> logger)
    : ICommandHandler<RegisterArtifactCommand, ErrorOr<RegisterArtifactResponse>>
{
    public async ValueTask<ErrorOr<RegisterArtifactResponse>> Handle(
        RegisterArtifactCommand command,
        CancellationToken cancellationToken)
    {
        var dataset = await repository.GetByIdAsync(command.DatasetId, cancellationToken);
        if (dataset is null)
            return DatasetErrors.NotFound(command.DatasetId);

        if (dataset.Status != DatasetStatus.InProgress)
            return DatasetErrors.InvalidStatus(command.DatasetId, dataset.Status, DatasetStatus.InProgress);

        var artifact = DatasetArtifact.Create(
            command.DatasetId,
            command.S3Key,
            command.ArtifactType,
            command.SizeBytes,
            command.RowCount,
            timeProvider.GetUtcNow());

        var saved = await repository.AddArtifactAsync(artifact, cancellationToken);

        logger.LogInformation(
            "Artifact {S3Key} registered for dataset {DatasetId}",
            command.S3Key, command.DatasetId);

        return new RegisterArtifactResponse(
            saved.Id,
            saved.DatasetId,
            saved.S3Key,
            saved.ArtifactType,
            saved.SizeBytes,
            saved.RowCount,
            saved.RegisteredAt);
    }
}

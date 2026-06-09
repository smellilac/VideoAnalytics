namespace VideoAnalytics.Application.Datasets.RegisterArtifact;

using ErrorOr;
using Mediator;

public sealed record RegisterArtifactCommand(
    Guid DatasetId,
    string S3Key,
    string ArtifactType,
    long SizeBytes,
    long RowCount) : ICommand<ErrorOr<RegisterArtifactResponse>>;

public sealed record RegisterArtifactResponse(
    Guid ArtifactId,
    Guid DatasetId,
    string S3Key,
    string ArtifactType,
    long SizeBytes,
    long RowCount,
    DateTimeOffset RegisteredAt);

namespace VideoAnalytics.Infrastructure.Storage;

using VideoAnalytics.Application.Interfaces;

// Null-object stub — replaced with MinIO HEAD-check when MinIO is wired up
internal sealed class NullArtifactStorage : IArtifactStorage
{
    public Task<bool> ExistsAsync(string s3Key, CancellationToken cancellationToken) =>
        Task.FromResult(true);
}

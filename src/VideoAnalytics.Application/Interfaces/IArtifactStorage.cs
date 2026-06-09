namespace VideoAnalytics.Application.Interfaces;

public interface IArtifactStorage
{
    Task<bool> ExistsAsync(string s3Key, CancellationToken cancellationToken);
}

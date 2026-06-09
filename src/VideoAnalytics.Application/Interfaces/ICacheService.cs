namespace VideoAnalytics.Application.Interfaces;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry, CancellationToken cancellationToken);
    Task InvalidateAsync(Guid datasetId, CancellationToken cancellationToken);
}

namespace VideoAnalytics.Infrastructure.Cache;

using VideoAnalytics.Application.Interfaces;

// Null-object stub — replaced with StackExchange.Redis when Redis is wired up
internal sealed class NullCacheService : ICacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken) =>
        Task.FromResult<T?>(default);

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task InvalidateAsync(Guid datasetId, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

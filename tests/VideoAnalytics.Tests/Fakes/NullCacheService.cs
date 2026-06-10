namespace VideoAnalytics.Tests.Fakes;

using VideoAnalytics.Application.Interfaces;

// No-op stand-in for tests — avoids needing a real Redis server in the test environment
internal sealed class NullCacheService : ICacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken) =>
        Task.FromResult<T?>(default);

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task InvalidateAsync(Guid datasetId, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

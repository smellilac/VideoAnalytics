namespace VideoAnalytics.Tests.Fakes;

using VideoAnalytics.Application.Interfaces;

// No-op stand-in for tests — avoids needing a real Kafka broker in the test environment
internal sealed class NullEventPublisher : IEventPublisher
{
    public Task PublishStatusChangedAsync(
        Guid datasetId, string fromStatus, string toStatus, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task PublishDatasetReadyAsync(Guid datasetId, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

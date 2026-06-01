namespace VideoAnalytics.Infrastructure.Kafka;

using Microsoft.Extensions.Logging;
using VideoAnalytics.Application.Interfaces;

// Null-object stub — replaced with Confluent.Kafka producer when Kafka is wired up
internal sealed class NullEventPublisher(ILogger<NullEventPublisher> logger) : IEventPublisher
{
    public Task PublishStatusChangedAsync(
        Guid datasetId, string fromStatus, string toStatus, CancellationToken cancellationToken)
    {
        logger.LogDebug(
            "dataset.status.changes — DatasetId: {DatasetId}, {From} → {To}",
            datasetId, fromStatus, toStatus);
        return Task.CompletedTask;
    }

    public Task PublishDatasetReadyAsync(Guid datasetId, CancellationToken cancellationToken)
    {
        logger.LogDebug("dataset.ready — DatasetId: {DatasetId}", datasetId);
        return Task.CompletedTask;
    }
}

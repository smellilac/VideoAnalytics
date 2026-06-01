namespace VideoAnalytics.Application.Interfaces;

public interface IEventPublisher
{
    Task PublishStatusChangedAsync(Guid datasetId, string fromStatus, string toStatus, CancellationToken cancellationToken);
    Task PublishDatasetReadyAsync(Guid datasetId, CancellationToken cancellationToken);
}

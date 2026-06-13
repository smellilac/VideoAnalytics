namespace VideoAnalytics.Application.Interfaces;

using VideoAnalytics.Domain.Datasets;
using VideoAnalytics.Domain.Outbox;

public interface IDatasetRepository
{
    Task<bool> ExistsAsync(string name, string version, CancellationToken cancellationToken);
    Task AddAsync(Dataset dataset, CancellationToken cancellationToken);
    Task<Dataset?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task UpdateAsync(Dataset dataset, CancellationToken cancellationToken);
    Task SaveTransitionAsync(Dataset dataset, DatasetStatusHistory history, OutboxMessage outboxMessage, CancellationToken cancellationToken);
    Task<DatasetArtifact> AddArtifactAsync(DatasetArtifact artifact, CancellationToken cancellationToken);
    Task<IReadOnlyList<DatasetArtifact>> GetArtifactsAsync(Guid datasetId, CancellationToken cancellationToken);
    Task<ReadinessResult> CheckReadinessAsync(Guid datasetId, CancellationToken cancellationToken);
    Task<bool> DependencyExistsAsync(Guid datasetId, Guid dependsOnDatasetId, CancellationToken cancellationToken);
    Task<bool> WouldCreateCycleAsync(Guid datasetId, Guid dependsOnDatasetId, CancellationToken cancellationToken);
    Task AddDependencyAsync(DatasetDependency dependency, CancellationToken cancellationToken);
    Task<(IReadOnlyList<Dataset> Items, int Total)> ListAsync(DatasetStatus? status, int skip, int take, CancellationToken cancellationToken);
    Task<IReadOnlyList<DatasetReadinessIssue>> CheckRangeReadinessAsync(string relevantName, DateOnly dateFrom, DateOnly dateTo, CancellationToken cancellationToken);
}

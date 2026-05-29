namespace VideoAnalytics.Application.Interfaces;

using VideoAnalytics.Domain.Datasets;

public interface IDatasetRepository
{
    Task<bool> ExistsAsync(string name, string version, CancellationToken cancellationToken);
    Task AddAsync(Dataset dataset, CancellationToken cancellationToken);
    Task<Dataset?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task UpdateAsync(Dataset dataset, CancellationToken cancellationToken);
    Task<ReadinessResult> CheckReadinessAsync(Guid datasetId, CancellationToken cancellationToken);
}

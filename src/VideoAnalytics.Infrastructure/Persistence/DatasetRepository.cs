namespace VideoAnalytics.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Npgsql;
using VideoAnalytics.Application.Interfaces;
using VideoAnalytics.Domain.Datasets;

public sealed class DatasetRepository(AppDbContext dbContext) : IDatasetRepository
{
    public async Task<bool> ExistsAsync(string name, string version, CancellationToken cancellationToken) =>
        await dbContext.Datasets.AnyAsync(d => d.Name == name && d.Version == version, cancellationToken);

    public async Task AddAsync(Dataset dataset, CancellationToken cancellationToken)
    {
        await dbContext.Datasets.AddAsync(dataset, cancellationToken);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            throw new InvalidOperationException(
                $"Dataset '{dataset.Name}' version '{dataset.Version}' already exists.", ex);
        }
    }

    public async Task<Dataset?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.Datasets.FindAsync([id], cancellationToken);

    public async Task UpdateAsync(Dataset dataset, CancellationToken cancellationToken)
    {
        dbContext.Datasets.Update(dataset);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<ReadinessResult> CheckReadinessAsync(Guid datasetId, CancellationToken cancellationToken)
    {
        // Implemented in CheckReadiness feature using Dapper recursive CTE
        throw new NotImplementedException();
    }
}

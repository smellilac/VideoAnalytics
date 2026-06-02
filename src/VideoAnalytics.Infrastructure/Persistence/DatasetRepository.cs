namespace VideoAnalytics.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Npgsql;
using VideoAnalytics.Application.Interfaces;
using VideoAnalytics.Domain.Datasets;
using VideoAnalytics.Domain.Outbox;

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

    public async Task SaveTransitionAsync(Dataset dataset, DatasetStatusHistory history, OutboxMessage outboxMessage, CancellationToken cancellationToken)
    {
        dbContext.Datasets.Update(dataset);
        await dbContext.DatasetStatusHistory.AddAsync(history, cancellationToken);
        await dbContext.OutboxMessages.AddAsync(outboxMessage, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<DatasetArtifact> AddArtifactAsync(DatasetArtifact artifact, CancellationToken cancellationToken)
    {
        dbContext.DatasetArtifacts.Add(artifact);
        
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return artifact;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            // ON CONFLICT (DatasetId, S3Key) DO NOTHING — return the already-registered artifact
            dbContext.ChangeTracker.Clear();
            return await dbContext.DatasetArtifacts
                .AsNoTracking()
                .SingleAsync(a => a.DatasetId == artifact.DatasetId && a.S3Key == artifact.S3Key, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<DatasetArtifact>> GetArtifactsAsync(Guid datasetId, CancellationToken cancellationToken) =>
        await dbContext.DatasetArtifacts
            .Where(a => a.DatasetId == datasetId)
            .ToListAsync(cancellationToken);

    public Task<ReadinessResult> CheckReadinessAsync(Guid datasetId, CancellationToken cancellationToken)
    {
        // Implemented in CheckReadiness feature using Dapper recursive CTE
        throw new NotImplementedException();
    }
}

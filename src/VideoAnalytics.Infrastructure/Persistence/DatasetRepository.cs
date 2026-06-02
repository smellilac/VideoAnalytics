namespace VideoAnalytics.Infrastructure.Persistence;

using Dapper;
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

    public async Task<ReadinessResult> CheckReadinessAsync(Guid datasetId, CancellationToken cancellationToken)
    {
        const string sql = """
            WITH RECURSIVE dep_tree AS (
                SELECT depends_on_dataset_id AS dependency_id
                FROM dataset_dependencies
                WHERE dataset_id = @datasetId
                UNION ALL
                SELECT dd.depends_on_dataset_id
                FROM dataset_dependencies dd
                INNER JOIN dep_tree dt ON dd.dataset_id = dt.dependency_id
            )
            SELECT d.name, d.version, d.status
            FROM datasets d
            INNER JOIN dep_tree dt ON d.id = dt.dependency_id
            WHERE d.status != 'Ready'
            LIMIT 1
            """;

        await using var conn = new NpgsqlConnection(dbContext.Database.GetConnectionString());
        var blocking = await conn.QueryFirstOrDefaultAsync<BlockingDependency>(
            new CommandDefinition(sql, new { datasetId }, cancellationToken: cancellationToken));

        return blocking is null
            ? ReadinessResult.Ready()
            : ReadinessResult.NotReady($"Dependency '{blocking.Name} v{blocking.Version}' is not ready (status: {blocking.Status})");
    }

    private sealed record BlockingDependency(string Name, string Version, string Status);
}

namespace VideoAnalytics.Infrastructure.Persistence;

using System.Data;
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
            dbContext.Entry(artifact).State = EntityState.Detached;
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
            ) CYCLE dependency_id SET is_cycle TO true DEFAULT false USING cycle_path
            SELECT dt.is_cycle AS "IsCycle", d.name, d.version, d.status
            FROM dep_tree dt
            INNER JOIN datasets d ON d.id = dt.dependency_id
            WHERE dt.is_cycle = true OR d.status != @readyStatus
            LIMIT 1
            """;

        var conn = dbContext.Database.GetDbConnection();
        if (conn.State == ConnectionState.Closed)
            await conn.OpenAsync(cancellationToken);

        var blocking = await conn.QueryFirstOrDefaultAsync<BlockingDependency>(
            new CommandDefinition(sql, new { datasetId, readyStatus = DatasetStatus.Ready.ToString() }, cancellationToken: cancellationToken));

        if (blocking is null)
            return ReadinessResult.Ready();

        if (blocking.IsCycle)
            return ReadinessResult.NotReady("Circular dependency detected in dependency graph.");

        return ReadinessResult.NotReady($"Dependency '{blocking.Name} v{blocking.Version}' is not ready (status: {blocking.Status})");
    }

    public async Task<bool> DependencyExistsAsync(Guid datasetId, Guid dependsOnDatasetId, CancellationToken cancellationToken) =>
        await dbContext.DatasetDependencies.AnyAsync(
            d => d.DatasetId == datasetId && d.DependsOnDatasetId == dependsOnDatasetId,
            cancellationToken);

    public async Task<bool> WouldCreateCycleAsync(Guid datasetId, Guid dependsOnDatasetId, CancellationToken cancellationToken)
    {
        // Walk all transitive dependencies starting from dependsOnDatasetId.
        // If we reach datasetId, adding this edge would create a cycle.
        const string sql = """
            WITH RECURSIVE reachable AS (
                SELECT depends_on_dataset_id AS node_id
                FROM dataset_dependencies
                WHERE dataset_id = @dependsOnDatasetId
                UNION ALL
                SELECT dd.depends_on_dataset_id
                FROM dataset_dependencies dd
                INNER JOIN reachable r ON dd.dataset_id = r.node_id
            ) CYCLE node_id SET is_cycle TO true DEFAULT false USING cycle_path
            SELECT EXISTS (
                SELECT 1 FROM reachable WHERE node_id = @datasetId AND is_cycle = false
            )
            """;

        var conn = dbContext.Database.GetDbConnection();
        if (conn.State == ConnectionState.Closed)
            await conn.OpenAsync(cancellationToken);

        return await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { datasetId, dependsOnDatasetId }, cancellationToken: cancellationToken));
    }

    public async Task AddDependencyAsync(DatasetDependency dependency, CancellationToken cancellationToken)
    {
        dbContext.DatasetDependencies.Add(dependency);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            // Concurrent request added the same (DatasetId, DependsOnDatasetId) — already exists, treat as success
            dbContext.Entry(dependency).State = EntityState.Detached;
        }
    }

    public async Task<(IReadOnlyList<Dataset> Items, int Total)> ListAsync(
        DatasetStatus? status, int skip, int take, CancellationToken cancellationToken)
    {
        var query = dbContext.Datasets.AsNoTracking();

        if (status is not null)
            query = query.Where(d => d.Status == status);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    private sealed record BlockingDependency(bool IsCycle, string? Name, string? Version, string? Status);
}

namespace VideoAnalytics.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using VideoAnalytics.Domain.Datasets;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Dataset> Datasets => Set<Dataset>();
    public DbSet<DatasetArtifact> DatasetArtifacts => Set<DatasetArtifact>();
    public DbSet<DatasetDependency> DatasetDependencies => Set<DatasetDependency>();
    public DbSet<DatasetStatusHistory> DatasetStatusHistory => Set<DatasetStatusHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}

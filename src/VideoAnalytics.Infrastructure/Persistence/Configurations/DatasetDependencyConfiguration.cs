namespace VideoAnalytics.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VideoAnalytics.Domain.Datasets;

public sealed class DatasetDependencyConfiguration : IEntityTypeConfiguration<DatasetDependency>
{
    public void Configure(EntityTypeBuilder<DatasetDependency> builder)
    {
        builder.HasKey(e => new { e.DatasetId, e.DependsOnDatasetId });

        builder.ToTable("dataset_dependencies");
    }
}

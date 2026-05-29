namespace VideoAnalytics.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VideoAnalytics.Domain.Datasets;

public sealed class DatasetArtifactConfiguration : IEntityTypeConfiguration<DatasetArtifact>
{
    public void Configure(EntityTypeBuilder<DatasetArtifact> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.DatasetId).IsRequired();

        builder.Property(e => e.S3Key).IsRequired();

        builder.Property(e => e.ArtifactType).IsRequired();

        builder.Property(e => e.SizeBytes).IsRequired();

        builder.Property(e => e.RowCount).IsRequired();

        builder.Property(e => e.RegisteredAt).IsRequired();

        builder.HasIndex(e => new { e.DatasetId, e.S3Key }).IsUnique();

        builder.ToTable("dataset_artifacts");
    }
}

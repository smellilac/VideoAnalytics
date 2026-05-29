namespace VideoAnalytics.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VideoAnalytics.Domain.Datasets;

public sealed class DatasetStatusHistoryConfiguration : IEntityTypeConfiguration<DatasetStatusHistory>
{
    public void Configure(EntityTypeBuilder<DatasetStatusHistory> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.DatasetId).IsRequired();

        builder.Property(e => e.FromStatus)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.ToStatus)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.Message);

        builder.Property(e => e.OccurredAt).IsRequired();

        builder.HasIndex(e => e.DatasetId);

        builder.ToTable("dataset_status_history");
    }
}

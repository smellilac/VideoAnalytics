namespace VideoAnalytics.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VideoAnalytics.Domain.Datasets;

public sealed class DatasetConfiguration : IEntityTypeConfiguration<Dataset>
{
    public void Configure(EntityTypeBuilder<Dataset> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Version)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.PipelineRunId)
            .IsRequired();

        builder.Property(e => e.ErrorMessage);

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.CreatedAt).IsRequired();

        builder.Property(e => e.UpdatedAt).IsRequired();

        builder.Property(e => e.CompletedAt);

        builder.Property(e => e.Metadata)
            .HasColumnType("jsonb");

        builder.HasIndex(e => new { e.Name, e.Version }).IsUnique();

        builder.ToTable("datasets");
    }
}

namespace VideoAnalytics.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VideoAnalytics.Domain.Outbox;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Type)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(m => m.Payload)
            .IsRequired();

        builder.Property(m => m.CreatedAt)
            .IsRequired();

        builder.Property(m => m.ProcessedAt);

        builder.HasIndex(m => m.ProcessedAt)
            .HasFilter("processed_at IS NULL");
    }
}

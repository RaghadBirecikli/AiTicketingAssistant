using AiTicketing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiTicketing.Infrastructure.Persistence.Configurations;

public sealed class TicketMessageConfiguration : IEntityTypeConfiguration<TicketMessage>
{
    public void Configure(EntityTypeBuilder<TicketMessage> builder)
    {
        builder.HasKey(message => message.Id);

        builder.Property(message => message.Message)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(message => message.IsInternalNote)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(message => message.CreatedByDisplayName)
            .HasMaxLength(150);

        builder.Property(message => message.CreatedAtUtc)
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasIndex(message => message.TicketId);
        builder.HasIndex(message => message.CreatedAtUtc);
    }
}

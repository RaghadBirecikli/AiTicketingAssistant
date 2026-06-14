using AiTicketing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiTicketing.Infrastructure.Persistence.Configurations;

public sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.HasKey(ticket => ticket.Id);

        builder.Property(ticket => ticket.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(ticket => ticket.Description)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(ticket => ticket.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(ticket => ticket.Priority)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(ticket => ticket.Category)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(ticket => ticket.Source)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(ticket => ticket.CustomerEmail)
            .HasMaxLength(256);

        builder.Property(ticket => ticket.CustomerName)
            .HasMaxLength(150);

        builder.Property(ticket => ticket.CustomerUserId)
            .HasMaxLength(100);

        builder.Property(ticket => ticket.CreatedAtUtc)
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(ticket => ticket.IsDeleted)
            .HasDefaultValue(false)
            .IsRequired();

        builder.HasMany(ticket => ticket.Messages)
            .WithOne(message => message.Ticket)
            .HasForeignKey(message => message.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(ticket => ticket.Status);
        builder.HasIndex(ticket => ticket.Priority);
        builder.HasIndex(ticket => ticket.CreatedAtUtc);
        builder.HasIndex(ticket => ticket.IsDeleted);
        builder.HasIndex(ticket => ticket.CustomerUserId);
    }
}

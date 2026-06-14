using AiTicketing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiTicketing.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(notification => notification.Id);

        builder.Property(notification => notification.UserId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(notification => notification.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(notification => notification.Message)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(notification => notification.Type)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(notification => notification.IsRead)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(notification => notification.CreatedAtUtc)
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasIndex(notification => notification.UserId);
        builder.HasIndex(notification => notification.IsRead);
        builder.HasIndex(notification => notification.CreatedAtUtc);
    }
}

using AiTicketing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiTicketing.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(auditLog => auditLog.Id);

        builder.Property(auditLog => auditLog.EntityName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(auditLog => auditLog.EntityId)
            .IsRequired();

        builder.Property(auditLog => auditLog.Action)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(auditLog => auditLog.OldValues)
            .HasColumnType("nvarchar(max)");

        builder.Property(auditLog => auditLog.NewValues)
            .HasColumnType("nvarchar(max)");

        builder.Property(auditLog => auditLog.PerformedByDisplayName)
            .HasMaxLength(150);

        builder.Property(auditLog => auditLog.PerformedAtUtc)
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasIndex(auditLog => new { auditLog.EntityName, auditLog.EntityId });
        builder.HasIndex(auditLog => auditLog.PerformedAtUtc);
    }
}

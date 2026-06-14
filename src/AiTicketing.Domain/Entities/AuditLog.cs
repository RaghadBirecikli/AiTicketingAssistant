namespace AiTicketing.Domain.Entities;

public sealed class AuditLog
{
    public Guid Id { get; set; }

    public string EntityName { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string? OldValues { get; set; }

    public string? NewValues { get; set; }

    public string? PerformedByUserId { get; set; }

    public string? PerformedByDisplayName { get; set; }

    public DateTime PerformedAtUtc { get; set; }
}

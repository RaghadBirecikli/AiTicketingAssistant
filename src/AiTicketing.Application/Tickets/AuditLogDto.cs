namespace AiTicketing.Application.Tickets;

public sealed record AuditLogDto(
    Guid Id,
    string EntityName,
    Guid EntityId,
    string Action,
    string? OldValues,
    string? NewValues,
    string? PerformedByUserId,
    string? PerformedByDisplayName,
    DateTimeOffset PerformedAtUtc);

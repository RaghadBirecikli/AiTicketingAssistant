namespace AiTicketing.Application.Tickets;

public sealed record TicketDetailsMessageDto(
    Guid Id,
    Guid TicketId,
    string? SenderUserId,
    string SenderRole,
    string? SenderDisplayName,
    string Body,
    bool IsInternalNote,
    DateTimeOffset CreatedAtUtc);

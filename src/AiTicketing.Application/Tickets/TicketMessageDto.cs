namespace AiTicketing.Application.Tickets;

public sealed record TicketMessageDto(
    Guid Id,
    Guid TicketId,
    string Message,
    bool IsInternalNote,
    string? CreatedByUserId,
    string? CreatedByDisplayName,
    DateTimeOffset CreatedAtUtc);

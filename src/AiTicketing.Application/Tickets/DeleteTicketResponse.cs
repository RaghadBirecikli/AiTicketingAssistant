namespace AiTicketing.Application.Tickets;

public sealed record DeleteTicketResponse(
    Guid TicketId,
    bool IsDeleted,
    DateTimeOffset DeletedAtUtc);

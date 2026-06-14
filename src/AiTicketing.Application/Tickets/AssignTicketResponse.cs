namespace AiTicketing.Application.Tickets;

public sealed record AssignTicketResponse(
    TicketDto Ticket,
    Guid TicketId,
    string AssignedToUserId,
    string? AssignedToDisplayName,
    string? AssignedByUserId,
    string? AssignedByDisplayName,
    DateTimeOffset AssignedAtUtc);

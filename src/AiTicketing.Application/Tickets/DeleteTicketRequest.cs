namespace AiTicketing.Application.Tickets;

public sealed record DeleteTicketRequest(
    string? DeletedByUserId,
    string? DeletedByDisplayName,
    string? Reason);

using AiTicketing.Domain.Enums;

namespace AiTicketing.Application.Tickets;

public sealed record ChangeTicketStatusRequest(
    TicketStatus Status,
    string? ChangedByUserId,
    string? ChangedByDisplayName);

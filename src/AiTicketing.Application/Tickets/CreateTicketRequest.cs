using AiTicketing.Domain.Enums;

namespace AiTicketing.Application.Tickets;

public sealed record CreateTicketRequest(
    string Title,
    string Description,
    string? CustomerEmail,
    string? CustomerName,
    TicketSource Source);

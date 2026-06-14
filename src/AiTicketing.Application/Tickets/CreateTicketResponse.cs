namespace AiTicketing.Application.Tickets;

public sealed record CreateTicketResponse(
    TicketDto Ticket,
    string AiSummary,
    string SuggestedReply);

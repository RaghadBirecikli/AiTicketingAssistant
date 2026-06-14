namespace AiTicketing.Application.Tickets;

public sealed record AddTicketMessageRequest(
    string Message,
    bool IsInternalNote,
    string? CreatedByUserId,
    string? CreatedByDisplayName);

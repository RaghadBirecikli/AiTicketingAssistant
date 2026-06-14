namespace AiTicketing.Application.Tickets;

public sealed record SummarizeTicketRequest(bool IncludeInternalNotes = false);

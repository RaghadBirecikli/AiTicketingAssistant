namespace AiTicketing.Application.Tickets;

public sealed record TicketStatsResponse(
    int Total,
    int Open,
    int InProgress,
    int Resolved,
    int Closed,
    int Unassigned,
    int LowPriority,
    int MediumPriority,
    int HighPriority,
    int UrgentPriority);

namespace AiTicketing.Application.Tickets;

public sealed record MyTicketStatsResponse(
    int Total,
    int Open,
    int InProgress,
    int Resolved,
    int Closed,
    int LowPriority,
    int MediumPriority,
    int HighPriority,
    int UrgentPriority);

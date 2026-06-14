namespace AiTicketing.Application.Dashboard;

public sealed record DashboardSummaryDto(
    int TotalTickets,
    int OpenTickets,
    int InProgressTickets,
    int WaitingForCustomerTickets,
    int ResolvedTickets,
    int ClosedTickets,
    int UrgentTickets,
    int UnassignedTickets,
    int TicketsCreatedToday,
    IReadOnlyList<DashboardCountItemDto> TicketsByStatus,
    IReadOnlyList<DashboardCountItemDto> TicketsByPriority,
    IReadOnlyList<DashboardCountItemDto> TicketsByCategory);

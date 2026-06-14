using AiTicketing.Application.Dashboard;
using AiTicketing.Domain.Enums;
using AiTicketing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiTicketing.Infrastructure.Dashboard;

public sealed class DashboardService(ApplicationDbContext dbContext) : IDashboardService
{
    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var todayUtc = DateTime.UtcNow.Date;
        var tomorrowUtc = todayUtc.AddDays(1);

        var tickets = dbContext.Tickets
            .AsNoTracking()
            .Where(ticket => !ticket.IsDeleted);

        var totalTickets = await tickets.CountAsync(cancellationToken);
        var openTickets = await tickets.CountAsync(ticket => ticket.Status == TicketStatus.Open, cancellationToken);
        var inProgressTickets = await tickets.CountAsync(ticket => ticket.Status == TicketStatus.InProgress, cancellationToken);
        var waitingForCustomerTickets = await tickets.CountAsync(ticket => ticket.Status == TicketStatus.WaitingForCustomer, cancellationToken);
        var resolvedTickets = await tickets.CountAsync(ticket => ticket.Status == TicketStatus.Resolved, cancellationToken);
        var closedTickets = await tickets.CountAsync(ticket => ticket.Status == TicketStatus.Closed, cancellationToken);
        var urgentTickets = await tickets.CountAsync(ticket => ticket.Priority == TicketPriority.Urgent, cancellationToken);
        var unassignedTickets = await tickets.CountAsync(ticket => ticket.AssignedToUserId == null, cancellationToken);
        var ticketsCreatedToday = await tickets.CountAsync(
            ticket => ticket.CreatedAtUtc >= todayUtc && ticket.CreatedAtUtc < tomorrowUtc,
            cancellationToken);

        var ticketsByStatus = await tickets
            .GroupBy(ticket => ticket.Status)
            .Select(group => new DashboardCountItemDto(group.Key.ToString(), group.Count()))
            .ToListAsync(cancellationToken);

        var ticketsByPriority = await tickets
            .GroupBy(ticket => ticket.Priority)
            .Select(group => new DashboardCountItemDto(group.Key.ToString(), group.Count()))
            .ToListAsync(cancellationToken);

        var ticketsByCategory = await tickets
            .GroupBy(ticket => ticket.Category)
            .Select(group => new DashboardCountItemDto(group.Key.ToString(), group.Count()))
            .ToListAsync(cancellationToken);

        return new DashboardSummaryDto(
            totalTickets,
            openTickets,
            inProgressTickets,
            waitingForCustomerTickets,
            resolvedTickets,
            closedTickets,
            urgentTickets,
            unassignedTickets,
            ticketsCreatedToday,
            ticketsByStatus,
            ticketsByPriority,
            ticketsByCategory);
    }
}

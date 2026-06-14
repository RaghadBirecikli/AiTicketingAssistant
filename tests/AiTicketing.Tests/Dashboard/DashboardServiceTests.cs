using AiTicketing.Application.Dashboard;
using AiTicketing.Domain.Entities;
using AiTicketing.Domain.Enums;
using AiTicketing.Infrastructure.Dashboard;
using AiTicketing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiTicketing.Tests.Dashboard;

public sealed class DashboardServiceTests
{
    [Fact]
    public async Task GetSummaryAsync_TotalTicketsExcludesDeletedTickets()
    {
        await using var dbContext = CreateDbContext();
        SeedTickets(dbContext,
            CreateTicket("Visible ticket"),
            CreateTicket("Deleted ticket", isDeleted: true));

        var summary = await new DashboardService(dbContext).GetSummaryAsync();

        Assert.Equal(1, summary.TotalTickets);
    }

    [Fact]
    public async Task GetSummaryAsync_CountsByStatus()
    {
        await using var dbContext = CreateDbContext();
        SeedTickets(dbContext,
            CreateTicket("Open", status: TicketStatus.Open),
            CreateTicket("In progress", status: TicketStatus.InProgress),
            CreateTicket("Waiting", status: TicketStatus.WaitingForCustomer),
            CreateTicket("Resolved", status: TicketStatus.Resolved),
            CreateTicket("Closed", status: TicketStatus.Closed));

        var summary = await new DashboardService(dbContext).GetSummaryAsync();

        Assert.Equal(1, summary.OpenTickets);
        Assert.Equal(1, summary.InProgressTickets);
        Assert.Equal(1, summary.WaitingForCustomerTickets);
        Assert.Equal(1, summary.ResolvedTickets);
        Assert.Equal(1, summary.ClosedTickets);
        Assert.Equal(1, CountFor(summary.TicketsByStatus, nameof(TicketStatus.Open)));
        Assert.Equal(1, CountFor(summary.TicketsByStatus, nameof(TicketStatus.Closed)));
    }

    [Fact]
    public async Task GetSummaryAsync_CountsByPriority()
    {
        await using var dbContext = CreateDbContext();
        SeedTickets(dbContext,
            CreateTicket("Low", priority: TicketPriority.Low),
            CreateTicket("Urgent one", priority: TicketPriority.Urgent),
            CreateTicket("Urgent two", priority: TicketPriority.Urgent));

        var summary = await new DashboardService(dbContext).GetSummaryAsync();

        Assert.Equal(2, summary.UrgentTickets);
        Assert.Equal(2, CountFor(summary.TicketsByPriority, nameof(TicketPriority.Urgent)));
        Assert.Equal(1, CountFor(summary.TicketsByPriority, nameof(TicketPriority.Low)));
    }

    [Fact]
    public async Task GetSummaryAsync_CountsByCategory()
    {
        await using var dbContext = CreateDbContext();
        SeedTickets(dbContext,
            CreateTicket("Bug one", category: TicketCategory.Bug),
            CreateTicket("Bug two", category: TicketCategory.Bug),
            CreateTicket("Billing", category: TicketCategory.Billing));

        var summary = await new DashboardService(dbContext).GetSummaryAsync();

        Assert.Equal(2, CountFor(summary.TicketsByCategory, nameof(TicketCategory.Bug)));
        Assert.Equal(1, CountFor(summary.TicketsByCategory, nameof(TicketCategory.Billing)));
    }

    [Fact]
    public async Task GetSummaryAsync_CountsUnassignedTickets()
    {
        await using var dbContext = CreateDbContext();
        SeedTickets(dbContext,
            CreateTicket("Unassigned one"),
            CreateTicket("Unassigned two"),
            CreateTicket("Assigned", assignedToUserId: "agent-1"));

        var summary = await new DashboardService(dbContext).GetSummaryAsync();

        Assert.Equal(2, summary.UnassignedTickets);
    }

    [Fact]
    public async Task GetSummaryAsync_TicketsCreatedTodayUsesUtcDate()
    {
        await using var dbContext = CreateDbContext();
        var todayUtc = DateTime.UtcNow.Date;

        SeedTickets(dbContext,
            CreateTicket("Today one", createdAtUtc: todayUtc.AddHours(1)),
            CreateTicket("Today two", createdAtUtc: todayUtc.AddHours(23).AddMinutes(59)),
            CreateTicket("Yesterday", createdAtUtc: todayUtc.AddTicks(-1)));

        var summary = await new DashboardService(dbContext).GetSummaryAsync();

        Assert.Equal(2, summary.TicketsCreatedToday);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static void SeedTickets(ApplicationDbContext dbContext, params Ticket[] tickets)
    {
        dbContext.Tickets.AddRange(tickets);
        dbContext.SaveChanges();
    }

    private static Ticket CreateTicket(
        string title,
        TicketStatus status = TicketStatus.Open,
        TicketPriority priority = TicketPriority.Medium,
        TicketCategory category = TicketCategory.General,
        string? assignedToUserId = null,
        DateTime? createdAtUtc = null,
        bool isDeleted = false) =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = $"{title} description",
            Status = status,
            Priority = priority,
            Category = category,
            Source = TicketSource.Web,
            AssignedToUserId = assignedToUserId,
            CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow,
            IsDeleted = isDeleted
        };

    private static int CountFor(IReadOnlyList<DashboardCountItemDto> items, string name) =>
        items.Single(item => item.Name == name).Count;
}

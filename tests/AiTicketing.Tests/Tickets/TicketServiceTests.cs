using AiTicketing.Application.Ai;
using AiTicketing.Application.Auth;
using AiTicketing.Application.Common.Interfaces;
using AiTicketing.Application.Notifications;
using AiTicketing.Application.Tickets;
using AiTicketing.Application.Tickets.Validation;
using AiTicketing.Application.Users;
using AiTicketing.Domain.Entities;
using AiTicketing.Domain.Enums;
using AiTicketing.Infrastructure.Notifications;
using AiTicketing.Infrastructure.Persistence;
using AiTicketing.Infrastructure.Tickets;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AiTicketing.Tests.Tickets;

public sealed class TicketServiceTests
{
    private const string AgentUserId = "11111111-1111-1111-1111-111111111111";
    private const string NewAgentUserId = "22222222-2222-2222-2222-222222222222";
    private const string CustomerUserId = "33333333-3333-3333-3333-333333333333";

    [Fact]
    public async Task CreateAsync_CreatesTicketAuditLogAndReturnsAiDetails()
    {
        await using var dbContext = CreateDbContext();
        var service = new TicketService(
            dbContext,
            new StubAiTicketAssistantService(),
            new CreateTicketRequestValidator(),
            new AddTicketMessageRequestValidator(),
            new AddInternalNoteRequestValidator(),
            new SuggestReplyRequestValidator(),
            new SuggestTriageRequestValidator(),
            new ChangeTicketStatusRequestValidator(),
            new AssignTicketRequestValidator(),
            new DeleteTicketRequestValidator(),
            new StubCurrentUserService(),
            new StubNotificationService(),
            new StubUserLookupService());

        var request = new CreateTicketRequest(
            "Payment failed",
            "The checkout payment failed with an error.",
            "customer@example.com",
            "Jane Customer",
            TicketSource.Web);

        var response = await service.CreateAsync(request);

        var ticket = await dbContext.Tickets.SingleAsync();
        var auditLog = await dbContext.AuditLogs.SingleAsync();

        Assert.Equal(ticket.Id, response.Ticket.Id);
        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(TicketCategory.Billing, ticket.Category);
        Assert.Equal(TicketSource.Web, ticket.Source);
        Assert.Equal("AI summary", response.AiSummary);
        Assert.Equal("Suggested reply", response.SuggestedReply);
        Assert.Equal(nameof(Ticket), auditLog.EntityName);
        Assert.Equal(ticket.Id, auditLog.EntityId);
        Assert.Equal("TicketCreated", auditLog.Action);
    }

    [Fact]
    public async Task CreateAsync_WhenAiCategoryIsUnknown_FallsBackToGeneralCategory()
    {
        await using var dbContext = CreateDbContext();
        var service = new TicketService(
            dbContext,
            new StubAiTicketAssistantService(suggestedCategory: "Unknown Category"),
            new CreateTicketRequestValidator(),
            new AddTicketMessageRequestValidator(),
            new AddInternalNoteRequestValidator(),
            new SuggestReplyRequestValidator(),
            new SuggestTriageRequestValidator(),
            new ChangeTicketStatusRequestValidator(),
            new AssignTicketRequestValidator(),
            new DeleteTicketRequestValidator(),
            new StubCurrentUserService(),
            new StubNotificationService(),
            new StubUserLookupService());

        var response = await service.CreateAsync(new CreateTicketRequest(
            "Question",
            "I need help with my account.",
            null,
            null,
            TicketSource.Internal));

        Assert.Equal(TicketCategory.General, response.Ticket.Category);
    }

    [Fact]
    public async Task CreateAsync_WhenAuthenticatedCustomer_SetsCustomerUserId()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "customer-user-1",
            email: "customer@example.com",
            fullName: "Customer User",
            role: "Customer"));

        var response = await service.CreateAsync(new CreateTicketRequest(
            "Customer issue",
            "Customer needs help with an issue.",
            "request@example.com",
            "Request Customer",
            TicketSource.Web));

        var ticket = await dbContext.Tickets.SingleAsync();

        Assert.Equal("customer-user-1", ticket.CustomerUserId);
        Assert.Equal("customer-user-1", response.Ticket.CustomerUserId);
    }

    [Fact]
    public async Task CreateAsync_WhenAuthenticatedCustomerAndRequestEmailIsNull_DefaultsCustomerEmailFromCurrentUser()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "customer-user-1",
            email: "customer@example.com",
            fullName: "Customer User",
            role: "Customer"));

        var response = await service.CreateAsync(new CreateTicketRequest(
            "Customer issue",
            "Customer needs help with an issue.",
            null,
            "Request Customer",
            TicketSource.Web));

        var ticket = await dbContext.Tickets.SingleAsync();

        Assert.Equal("customer@example.com", ticket.CustomerEmail);
        Assert.Equal("customer@example.com", response.Ticket.CustomerEmail);
    }

    [Fact]
    public async Task CreateAsync_WhenAuthenticatedCustomerAndRequestNameIsNull_DefaultsCustomerNameFromCurrentUser()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "customer-user-1",
            email: "customer@example.com",
            fullName: "Customer User",
            role: "Customer"));

        var response = await service.CreateAsync(new CreateTicketRequest(
            "Customer issue",
            "Customer needs help with an issue.",
            "request@example.com",
            null,
            TicketSource.Web));

        var ticket = await dbContext.Tickets.SingleAsync();

        Assert.Equal("Customer User", ticket.CustomerName);
        Assert.Equal("Customer User", response.Ticket.CustomerName);
    }

    [Fact]
    public async Task CreateAsync_WhenAuthenticatedAdmin_DoesNotSetCustomerUserId()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "admin-user-1",
            email: "admin@example.com",
            fullName: "Admin User",
            role: "Admin"));

        var response = await service.CreateAsync(new CreateTicketRequest(
            "Admin-created issue",
            "Admin creates ticket without ownership for now.",
            "customer@example.com",
            "Customer User",
            TicketSource.Internal));

        var ticket = await dbContext.Tickets.SingleAsync();

        Assert.Null(ticket.CustomerUserId);
        Assert.Null(response.Ticket.CustomerUserId);
    }

    [Fact]
    public async Task CreateAsync_WhenUnauthenticated_DoesNotSetCustomerUserId()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var response = await service.CreateAsync(new CreateTicketRequest(
            "Anonymous issue",
            "Anonymous customer creates a ticket.",
            "customer@example.com",
            "Customer User",
            TicketSource.Web));

        var ticket = await dbContext.Tickets.SingleAsync();

        Assert.Null(ticket.CustomerUserId);
        Assert.Null(response.Ticket.CustomerUserId);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsCustomerUserIdInDetailsDto()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Customer owned ticket");
        ticket.CustomerUserId = "customer-user-1";
        SeedTickets(dbContext, ticket);

        var service = CreateAdminService(dbContext);

        var result = await service.GetByIdAsync(ticket.Id);

        Assert.NotNull(result);
        Assert.Equal("customer-user-1", result.CustomerUserId);
    }

    [Fact]
    public async Task GetListAsync_ReturnsCustomerUserIdInTicketDto()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Customer owned list ticket");
        ticket.CustomerUserId = "customer-user-1";
        SeedTickets(dbContext, ticket);

        var service = CreateAdminService(dbContext);

        var result = await service.GetListAsync(new GetTicketsQuery());

        Assert.Single(result.Items);
        Assert.Equal("customer-user-1", result.Items[0].CustomerUserId);
    }

    [Fact]
    public async Task GetListAsync_ReturnsPagedResultsSortedByCreatedAtDescending()
    {
        await using var dbContext = CreateDbContext();
        SeedTickets(dbContext,
            CreateTicket("Old ticket", createdAtUtc: DateTime.UtcNow.AddDays(-2)),
            CreateTicket("Newest ticket", createdAtUtc: DateTime.UtcNow),
            CreateTicket("Middle ticket", createdAtUtc: DateTime.UtcNow.AddDays(-1)));

        var service = CreateAdminService(dbContext);

        var result = await service.GetListAsync(new GetTicketsQuery { PageSize = 2 });

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.False(result.HasPreviousPage);
        Assert.True(result.HasNextPage);
        Assert.Equal("Newest ticket", result.Items[0].Title);
        Assert.Equal("Middle ticket", result.Items[1].Title);
    }

    [Fact]
    public async Task GetListAsync_FiltersByStatus()
    {
        await using var dbContext = CreateDbContext();
        SeedTickets(dbContext,
            CreateTicket("Open ticket", status: TicketStatus.Open),
            CreateTicket("Resolved ticket", status: TicketStatus.Resolved));

        var service = CreateAdminService(dbContext);

        var result = await service.GetListAsync(new GetTicketsQuery { Status = TicketStatus.Resolved });

        Assert.Single(result.Items);
        Assert.Equal("Resolved ticket", result.Items[0].Title);
        Assert.Equal(TicketStatus.Resolved, result.Items[0].Status);
    }

    [Fact]
    public async Task GetListAsync_SearchesByTitleAndDescriptionOnly()
    {
        await using var dbContext = CreateDbContext();
        SeedTickets(dbContext,
            CreateTicket("Payment page issue", customerEmail: "sara@example.com"),
            CreateTicket("Login problem", description: "Password reset fails.", customerEmail: "customer@example.com"),
            CreateTicket("Feature question", customerEmail: "another@example.com"));

        var service = CreateAdminService(dbContext);

        var titleResult = await service.GetListAsync(new GetTicketsQuery { Search = "payment" });
        var descriptionResult = await service.GetListAsync(new GetTicketsQuery { Search = "password" });
        var emailResult = await service.GetListAsync(new GetTicketsQuery { Search = "customer@example.com" });

        Assert.Single(titleResult.Items);
        Assert.Equal("Payment page issue", titleResult.Items[0].Title);
        Assert.Single(descriptionResult.Items);
        Assert.Equal("Login problem", descriptionResult.Items[0].Title);
        Assert.Empty(emailResult.Items);
    }

    [Fact]
    public async Task GetListAsync_ExcludesSoftDeletedTickets()
    {
        await using var dbContext = CreateDbContext();
        SeedTickets(dbContext,
            CreateTicket("Visible ticket"),
            CreateTicket("Deleted ticket", isDeleted: true));

        var service = CreateAdminService(dbContext);

        var result = await service.GetListAsync(new GetTicketsQuery());

        Assert.Single(result.Items);
        Assert.Equal("Visible ticket", result.Items[0].Title);
    }

    [Fact]
    public async Task GetListAsync_WhenPaginationIsInvalid_ThrowsValidationException()
    {
        await using var dbContext = CreateDbContext();
        SeedTickets(dbContext, CreateTicket("Visible ticket"));

        var service = CreateAdminService(dbContext);

        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            service.GetListAsync(new GetTicketsQuery { Page = 0, PageSize = 500 }));
    }

    [Fact]
    public async Task GetStatsAsync_ReturnsCurrentNonDeletedTicketCounts()
    {
        await using var dbContext = CreateDbContext();
        var emptyAssignment = CreateTicket("Empty assignment", status: TicketStatus.Open, priority: TicketPriority.Low);
        var inProgress = CreateTicket("In progress", status: TicketStatus.InProgress, priority: TicketPriority.Medium);
        var closed = CreateTicket("Closed", status: TicketStatus.Closed, priority: TicketPriority.Urgent);
        emptyAssignment.AssignedToUserId = string.Empty;
        inProgress.AssignedToUserId = "agent-1";
        closed.AssignedToUserId = "agent-2";

        SeedTickets(dbContext,
            emptyAssignment,
            inProgress,
            CreateTicket("Resolved", status: TicketStatus.Resolved, priority: TicketPriority.High),
            closed,
            CreateTicket("Deleted urgent", status: TicketStatus.Open, priority: TicketPriority.Urgent, isDeleted: true));

        var service = CreateAdminService(dbContext);

        var result = await service.GetStatsAsync();

        Assert.Equal(4, result.Total);
        Assert.Equal(1, result.Open);
        Assert.Equal(1, result.InProgress);
        Assert.Equal(1, result.Resolved);
        Assert.Equal(1, result.Closed);
        Assert.Equal(2, result.Unassigned);
        Assert.Equal(1, result.LowPriority);
        Assert.Equal(1, result.MediumPriority);
        Assert.Equal(1, result.HighPriority);
        Assert.Equal(1, result.UrgentPriority);
    }

    [Fact]
    public async Task GetMyStatsAsync_WhenAgent_ReturnsOnlyAssignedNonDeletedTicketCounts()
    {
        await using var dbContext = CreateDbContext();
        var assignedOpen = CreateTicket("Assigned open", status: TicketStatus.Open, priority: TicketPriority.Low);
        var assignedResolved = CreateTicket("Assigned resolved", status: TicketStatus.Resolved, priority: TicketPriority.High);
        var otherAgent = CreateTicket("Other agent", status: TicketStatus.Closed, priority: TicketPriority.Urgent);
        var deletedAssigned = CreateTicket(
            "Deleted assigned",
            status: TicketStatus.InProgress,
            priority: TicketPriority.Medium,
            isDeleted: true);
        assignedOpen.AssignedToUserId = "agent-user";
        assignedResolved.AssignedToUserId = "agent-user";
        otherAgent.AssignedToUserId = "other-agent";
        deletedAssigned.AssignedToUserId = "agent-user";
        SeedTickets(dbContext, assignedOpen, assignedResolved, otherAgent, deletedAssigned);

        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "agent-user",
            role: AuthRoles.Agent));

        var result = await service.GetMyStatsAsync();

        Assert.Equal(2, result.Total);
        Assert.Equal(1, result.Open);
        Assert.Equal(0, result.InProgress);
        Assert.Equal(1, result.Resolved);
        Assert.Equal(0, result.Closed);
        Assert.Equal(1, result.LowPriority);
        Assert.Equal(0, result.MediumPriority);
        Assert.Equal(1, result.HighPriority);
        Assert.Equal(0, result.UrgentPriority);
    }

    [Fact]
    public async Task GetMyStatsAsync_WhenCustomer_ReturnsOnlyOwnedNonDeletedTicketCounts()
    {
        await using var dbContext = CreateDbContext();
        var ownedInProgress = CreateTicket(
            "Owned in progress",
            status: TicketStatus.InProgress,
            priority: TicketPriority.Medium);
        var ownedClosed = CreateTicket("Owned closed", status: TicketStatus.Closed, priority: TicketPriority.Urgent);
        var otherCustomer = CreateTicket("Other customer", status: TicketStatus.Open, priority: TicketPriority.Low);
        var deletedOwned = CreateTicket(
            "Deleted owned",
            status: TicketStatus.Resolved,
            priority: TicketPriority.High,
            isDeleted: true);
        ownedInProgress.CustomerUserId = "customer-user";
        ownedClosed.CustomerUserId = "customer-user";
        otherCustomer.CustomerUserId = "other-customer";
        deletedOwned.CustomerUserId = "customer-user";
        SeedTickets(dbContext, ownedInProgress, ownedClosed, otherCustomer, deletedOwned);

        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "customer-user",
            role: AuthRoles.Customer));

        var result = await service.GetMyStatsAsync();

        Assert.Equal(2, result.Total);
        Assert.Equal(0, result.Open);
        Assert.Equal(1, result.InProgress);
        Assert.Equal(0, result.Resolved);
        Assert.Equal(1, result.Closed);
        Assert.Equal(0, result.LowPriority);
        Assert.Equal(1, result.MediumPriority);
        Assert.Equal(0, result.HighPriority);
        Assert.Equal(1, result.UrgentPriority);
    }

    [Fact]
    public async Task GetByIdAsync_WhenTicketExists_ReturnsTicketWithMessages()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Ticket with messages");
        ticket.Messages.Add(CreateMessage(ticket.Id, "Second message", DateTime.UtcNow.AddMinutes(2)));
        ticket.Messages.Add(CreateMessage(ticket.Id, "First message", DateTime.UtcNow.AddMinutes(1)));
        SeedTickets(dbContext, ticket);

        var service = CreateAdminService(dbContext);

        var result = await service.GetByIdAsync(ticket.Id);

        Assert.NotNull(result);
        Assert.Equal(ticket.Id, result.Id);
        Assert.Equal(2, result.Messages.Count);
        Assert.Equal("First message", result.Messages[0].Body);
        Assert.Equal("Second message", result.Messages[1].Body);
    }

    [Fact]
    public async Task GetByIdAsync_WhenTicketDoesNotExist_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateAdminService(dbContext);

        var result = await service.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_WhenTicketIsSoftDeleted_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Deleted ticket", isDeleted: true);
        SeedTickets(dbContext, ticket);

        var service = CreateAdminService(dbContext);

        var result = await service.GetByIdAsync(ticket.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_OrdersMessagesByCreatedAtAscending()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Ordered messages");
        ticket.Messages.Add(CreateMessage(ticket.Id, "Newest", DateTime.UtcNow.AddHours(2)));
        ticket.Messages.Add(CreateMessage(ticket.Id, "Oldest", DateTime.UtcNow));
        ticket.Messages.Add(CreateMessage(ticket.Id, "Middle", DateTime.UtcNow.AddHours(1)));
        SeedTickets(dbContext, ticket);

        var service = CreateAdminService(dbContext);

        var result = await service.GetByIdAsync(ticket.Id);

        Assert.NotNull(result);
        Assert.Collection(
            result.Messages,
            message => Assert.Equal("Oldest", message.Body),
            message => Assert.Equal("Middle", message.Body),
            message => Assert.Equal("Newest", message.Body));
    }

    [Fact]
    public async Task AddMessageAsync_AddsNormalMessageUpdatesTicketAndCreatesAuditLog()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Ticket with reply");
        SeedTickets(dbContext, ticket);

        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "admin-user",
            email: "admin@example.com",
            fullName: "Admin User",
            role: AuthRoles.Admin));

        var response = await service.AddMessageAsync(ticket.Id, new AddTicketMessageRequest(
            "Customer replied with more details.",
            false,
            "user-1",
            "Support Agent"));

        var savedTicket = await dbContext.Tickets.SingleAsync();
        var savedMessage = await dbContext.TicketMessages.SingleAsync();
        var auditLog = await dbContext.AuditLogs.SingleAsync();

        Assert.NotNull(response);
        Assert.Equal(savedMessage.Id, response.Message.Id);
        Assert.Equal("Customer replied with more details.", savedMessage.Message);
        Assert.False(savedMessage.IsInternalNote);
        Assert.NotNull(savedTicket.UpdatedAtUtc);
        Assert.Equal(nameof(TicketMessage), auditLog.EntityName);
        Assert.Equal(savedMessage.Id, auditLog.EntityId);
        Assert.Equal("MessageAdded", auditLog.Action);
        Assert.Equal("admin-user", auditLog.PerformedByUserId);
        Assert.Equal("Admin User", auditLog.PerformedByDisplayName);
    }

    [Fact]
    public async Task AddMessageAsync_WhenUserIsAuthenticated_UsesCurrentUserForMessageCreatorFields()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Ticket authenticated message creator");
        ticket.AssignedToUserId = "authenticated-user";
        SeedTickets(dbContext, ticket);

        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "authenticated-user",
            email: "agent@example.com",
            fullName: "Authenticated Agent",
            role: "Agent"));

        await service.AddMessageAsync(ticket.Id, new AddTicketMessageRequest(
            "Authenticated message.",
            false,
            "request-user",
            "Request User"));

        var savedMessage = await dbContext.TicketMessages.SingleAsync();

        Assert.Equal("authenticated-user", savedMessage.CreatedByUserId);
        Assert.Equal("Authenticated Agent", savedMessage.CreatedByDisplayName);
    }

    [Fact]
    public async Task AddMessageAsync_WhenUserIsAuthenticated_UsesCurrentUserForAuditFields()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Ticket authenticated message audit");
        ticket.AssignedToUserId = "authenticated-user";
        SeedTickets(dbContext, ticket);

        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "authenticated-user",
            email: "agent@example.com",
            fullName: "Authenticated Agent",
            role: "Agent"));

        await service.AddMessageAsync(ticket.Id, new AddTicketMessageRequest(
            "Authenticated message.",
            false,
            "request-user",
            "Request User"));

        var auditLog = await dbContext.AuditLogs.SingleAsync();

        Assert.Equal("authenticated-user", auditLog.PerformedByUserId);
        Assert.Equal("Authenticated Agent", auditLog.PerformedByDisplayName);
    }

    [Fact]
    public async Task AddMessageAsync_WhenUserIsUnauthenticated_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Ticket anonymous message audit");
        SeedTickets(dbContext, ticket);

        var service = CreateService(dbContext, new StubCurrentUserService());

        var response = await service.AddMessageAsync(ticket.Id, new AddTicketMessageRequest(
            "Anonymous message.",
            false,
            "request-user",
            "Request User"));

        Assert.Null(response);
        Assert.Empty(dbContext.TicketMessages);
        Assert.Empty(dbContext.AuditLogs);
    }

    [Fact]
    public async Task AddMessageAsync_AddsInternalNoteWithInternalAuditAction()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Ticket with internal note");
        SeedTickets(dbContext, ticket);

        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "admin-user",
            email: "admin@example.com",
            fullName: "Admin User",
            role: AuthRoles.Admin));

        var response = await service.AddMessageAsync(ticket.Id, new AddTicketMessageRequest(
            "Escalated to billing team.",
            true,
            null,
            "Support Lead"));

        var auditLog = await dbContext.AuditLogs.SingleAsync();

        Assert.NotNull(response);
        Assert.True(response.Message.IsInternalNote);
        Assert.Equal("InternalNoteAdded", auditLog.Action);
    }

    [Fact]
    public async Task AddInternalNoteAsync_WhenAdmin_AddsTrimmedNoteWithoutNotification()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Admin internal note");
        ticket.CustomerUserId = "customer-user";
        SeedTickets(dbContext, ticket);
        var notificationService = new StubNotificationService();
        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "admin-user",
            email: "admin@example.com",
            fullName: "Admin User",
            role: AuthRoles.Admin), notificationService);

        var response = await service.AddInternalNoteAsync(ticket.Id, new AddInternalNoteRequest(
            "  Escalate to billing.  "));

        var message = await dbContext.TicketMessages.SingleAsync();
        var auditLog = await dbContext.AuditLogs.SingleAsync();
        Assert.NotNull(response);
        Assert.Equal("Escalate to billing.", message.Message);
        Assert.True(message.IsInternalNote);
        Assert.Equal("admin-user", message.CreatedByUserId);
        Assert.True(response.Message.IsInternalNote);
        Assert.Equal("Escalate to billing.", response.Message.Body);
        Assert.Equal(AuthRoles.Admin, response.Message.SenderRole);
        Assert.Equal("InternalNoteAdded", auditLog.Action);
        Assert.Empty(notificationService.Requests);
    }

    [Fact]
    public async Task AddInternalNoteAsync_WhenAssignedAgent_AddsNote()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Agent internal note");
        ticket.AssignedToUserId = "agent-user";
        ticket.CustomerUserId = "customer-user";
        ticket.CustomerUserId = "customer-user";
        SeedTickets(dbContext, ticket);
        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "agent-user",
            fullName: "Agent User",
            role: AuthRoles.Agent));

        var response = await service.AddInternalNoteAsync(ticket.Id, new AddInternalNoteRequest("Agent-only context."));

        Assert.NotNull(response);
        Assert.Equal(AuthRoles.Agent, response.Message.SenderRole);
        Assert.True(response.Message.IsInternalNote);
    }

    [Fact]
    public async Task AddInternalNoteAsync_WhenAgentIsNotAssigned_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Other agent internal note");
        ticket.AssignedToUserId = "other-agent";
        SeedTickets(dbContext, ticket);
        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "agent-user",
            role: AuthRoles.Agent));

        var response = await service.AddInternalNoteAsync(ticket.Id, new AddInternalNoteRequest("Hidden context."));

        Assert.Null(response);
        Assert.Empty(dbContext.TicketMessages);
        Assert.Empty(dbContext.AuditLogs);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AddInternalNoteAsync_WhenBodyIsEmpty_ThrowsValidationException(string body)
    {
        await using var dbContext = CreateDbContext();
        var service = CreateAdminService(dbContext);

        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            service.AddInternalNoteAsync(Guid.NewGuid(), new AddInternalNoteRequest(body)));
    }

    [Fact]
    public async Task GetByIdAsync_HidesInternalNotesFromCustomerButShowsThemToAdminAndAssignedAgent()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Internal note visibility");
        ticket.CustomerUserId = "customer-user";
        ticket.AssignedToUserId = "agent-user";
        ticket.Messages.Add(CreateMessage(ticket.Id, "Public message", DateTime.UtcNow.AddMinutes(-2)));
        ticket.Messages.Add(CreateMessage(
            ticket.Id,
            "Staff-only note",
            DateTime.UtcNow.AddMinutes(-1),
            isInternalNote: true));
        SeedTickets(dbContext, ticket);

        var adminResult = await CreateAdminService(dbContext).GetByIdAsync(ticket.Id);
        var agentResult = await CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "agent-user",
            role: AuthRoles.Agent)).GetByIdAsync(ticket.Id);
        var customerResult = await CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "customer-user",
            role: AuthRoles.Customer)).GetByIdAsync(ticket.Id);

        Assert.NotNull(adminResult);
        Assert.NotNull(agentResult);
        Assert.NotNull(customerResult);
        Assert.Equal(["Public message", "Staff-only note"], adminResult.Messages.Select(message => message.Body));
        Assert.Equal(["Public message", "Staff-only note"], agentResult.Messages.Select(message => message.Body));
        Assert.Equal(["Public message"], customerResult.Messages.Select(message => message.Body));
    }

    [Fact]
    public async Task SuggestReplyAsync_WhenAdmin_PassesVisibleTicketContextAndMakesNoChanges()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket(
            "Payment failure",
            status: TicketStatus.InProgress,
            priority: TicketPriority.High,
            description: "Customer cannot finish checkout.");
        ticket.CustomerUserId = "customer-user";
        ticket.Messages.Add(CreateMessage(ticket.Id, "Customer-visible first", DateTime.UtcNow.AddMinutes(-3)));
        ticket.Messages.Add(CreateMessage(ticket.Id, "Secret internal note", DateTime.UtcNow.AddMinutes(-2), isInternalNote: true));
        ticket.Messages.Add(CreateMessage(ticket.Id, "Customer-visible second", DateTime.UtcNow.AddMinutes(-1)));
        SeedTickets(dbContext, ticket);
        var aiService = new StubAiTicketAssistantService();
        var notificationService = new StubNotificationService();
        var service = CreateService(
            dbContext,
            new StubCurrentUserService(isAuthenticated: true, userId: "admin-user", role: AuthRoles.Admin),
            notificationService,
            aiService);

        var response = await service.SuggestReplyAsync(ticket.Id, new SuggestReplyRequest("  Keep it concise.  "));

        Assert.NotNull(response);
        Assert.Equal("Suggested customer reply.", response.SuggestedReply);
        var aiRequest = Assert.IsType<TicketReplySuggestionRequest>(aiService.LastReplySuggestionRequest);
        Assert.Equal("Payment failure", aiRequest.Title);
        Assert.Equal("Customer cannot finish checkout.", aiRequest.Description);
        Assert.Equal("InProgress", aiRequest.Status);
        Assert.Equal("High", aiRequest.Priority);
        Assert.Equal("Keep it concise.", aiRequest.Instruction);
        Assert.Equal(
            ["Customer-visible first", "Customer-visible second"],
            aiRequest.Messages.Select(message => message.Body));
        Assert.DoesNotContain(aiRequest.Messages, message => message.Body == "Secret internal note");
        Assert.Empty(notificationService.Requests);
        Assert.Empty(dbContext.AuditLogs);
        Assert.Equal(3, await dbContext.TicketMessages.CountAsync());
        Assert.Equal(TicketStatus.InProgress, (await dbContext.Tickets.SingleAsync()).Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SuggestReplyAsync_WhenInstructionIsEmpty_PassesNull(string? instruction)
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Instruction normalization");
        SeedTickets(dbContext, ticket);
        var aiService = new StubAiTicketAssistantService();
        var service = CreateService(
            dbContext,
            new StubCurrentUserService(isAuthenticated: true, userId: "admin-user", role: AuthRoles.Admin),
            aiTicketAssistantService: aiService);

        var response = await service.SuggestReplyAsync(ticket.Id, new SuggestReplyRequest(instruction));

        Assert.NotNull(response);
        Assert.Null(aiService.LastReplySuggestionRequest?.Instruction);
    }

    [Fact]
    public async Task SuggestReplyAsync_WhenInstructionIsOverlong_ThrowsValidationException()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateAdminService(dbContext);

        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            service.SuggestReplyAsync(Guid.NewGuid(), new SuggestReplyRequest(new string('x', 501))));
    }

    [Fact]
    public async Task SuggestReplyAsync_WhenAgentIsAssigned_ReturnsSuggestion()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Assigned agent suggestion");
        ticket.AssignedToUserId = "agent-user";
        SeedTickets(dbContext, ticket);
        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "agent-user",
            role: AuthRoles.Agent));

        var response = await service.SuggestReplyAsync(ticket.Id, new SuggestReplyRequest());

        Assert.NotNull(response);
    }

    [Fact]
    public async Task SuggestReplyAsync_WhenTicketIsInaccessible_ReturnsNullWithoutCallingAi()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Inaccessible suggestion");
        ticket.AssignedToUserId = "other-agent";
        SeedTickets(dbContext, ticket);
        var aiService = new StubAiTicketAssistantService();
        var service = CreateService(
            dbContext,
            new StubCurrentUserService(isAuthenticated: true, userId: "agent-user", role: AuthRoles.Agent),
            aiTicketAssistantService: aiService);

        var response = await service.SuggestReplyAsync(ticket.Id, new SuggestReplyRequest());

        Assert.Null(response);
        Assert.Null(aiService.LastReplySuggestionRequest);
    }

    [Fact]
    public async Task SummarizeAsync_WhenAdmin_DefaultExcludesInternalNotesAndMakesNoChanges()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket(
            "Payment summary",
            status: TicketStatus.InProgress,
            priority: TicketPriority.High,
            description: "Customer cannot finish checkout.");
        ticket.AssignedToUserId = "agent-user";
        ticket.CustomerUserId = "customer-user";
        var olderMessage = CreateMessage(ticket.Id, "Older visible message", DateTime.UtcNow.AddMinutes(-3));
        olderMessage.CreatedByUserId = "customer-user";
        olderMessage.CreatedByDisplayName = "Sara Ahmed";
        var internalNote = CreateMessage(ticket.Id, "Secret internal note", DateTime.UtcNow.AddMinutes(-2), isInternalNote: true);
        internalNote.CreatedByUserId = "agent-user";
        internalNote.CreatedByDisplayName = "Support Agent";
        var newerMessage = CreateMessage(ticket.Id, "Newer visible message", DateTime.UtcNow.AddMinutes(-1));
        newerMessage.CreatedByUserId = "agent-user";
        newerMessage.CreatedByDisplayName = "Support Agent";
        ticket.Messages.Add(olderMessage);
        ticket.Messages.Add(internalNote);
        ticket.Messages.Add(newerMessage);
        SeedTickets(dbContext, ticket);
        var aiService = new StubAiTicketAssistantService();
        var notificationService = new StubNotificationService();
        var service = CreateService(
            dbContext,
            new StubCurrentUserService(isAuthenticated: true, userId: "admin-user", role: AuthRoles.Admin),
            notificationService,
            aiService);

        var response = await service.SummarizeAsync(ticket.Id, new SummarizeTicketRequest());

        Assert.NotNull(response);
        Assert.Equal("Operational ticket summary.", response.Summary);
        var aiRequest = Assert.IsType<TicketSummaryRequest>(aiService.LastSummaryRequest);
        Assert.Equal("Payment summary", aiRequest.Title);
        Assert.Equal("Customer cannot finish checkout.", aiRequest.Description);
        Assert.Equal("InProgress", aiRequest.Status);
        Assert.Equal("High", aiRequest.Priority);
        Assert.False(aiRequest.IncludesInternalNotes);
        Assert.Equal("Support Agent", aiRequest.AssignedAgentDisplayName);
        Assert.Equal(["Older visible message", "Newer visible message"], aiRequest.Messages.Select(message => message.Body));
        Assert.Equal([AuthRoles.Customer, AuthRoles.Agent], aiRequest.Messages.Select(message => message.SenderRole));
        Assert.Equal(["Sara Ahmed", "Support Agent"], aiRequest.Messages.Select(message => message.SenderDisplayName));
        Assert.All(aiRequest.Messages, message => Assert.Equal(TimeSpan.Zero, message.CreatedAtUtc.Offset));
        Assert.DoesNotContain(aiRequest.Messages, message => message.IsInternalNote);
        Assert.Empty(notificationService.Requests);
        Assert.Empty(dbContext.AuditLogs);
        Assert.Equal(3, await dbContext.TicketMessages.CountAsync());
        var savedTicket = await dbContext.Tickets.SingleAsync();
        Assert.Equal(TicketStatus.InProgress, savedTicket.Status);
        Assert.Equal(TicketPriority.High, savedTicket.Priority);
        Assert.Equal("agent-user", savedTicket.AssignedToUserId);
    }

    [Fact]
    public async Task SummarizeAsync_WhenAdminIncludesInternalNotes_PassesLabeledInternalNotesToAi()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Summary with internal notes");
        ticket.Messages.Add(CreateMessage(ticket.Id, "Visible message", DateTime.UtcNow.AddMinutes(-2)));
        ticket.Messages.Add(CreateMessage(ticket.Id, "Internal escalation note", DateTime.UtcNow.AddMinutes(-1), isInternalNote: true));
        SeedTickets(dbContext, ticket);
        var aiService = new StubAiTicketAssistantService();
        var service = CreateService(
            dbContext,
            new StubCurrentUserService(isAuthenticated: true, userId: "admin-user", role: AuthRoles.Admin),
            aiTicketAssistantService: aiService);

        var response = await service.SummarizeAsync(ticket.Id, new SummarizeTicketRequest(IncludeInternalNotes: true));

        Assert.NotNull(response);
        var aiRequest = Assert.IsType<TicketSummaryRequest>(aiService.LastSummaryRequest);
        Assert.True(aiRequest.IncludesInternalNotes);
        Assert.Contains(aiRequest.Messages, message => message.Body == "Internal escalation note" && message.IsInternalNote);
    }

    [Fact]
    public async Task SummarizeAsync_WhenAssignedAgentRequestsInternalNotes_ReturnsNullWithoutCallingAi()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Agent internal summary");
        ticket.AssignedToUserId = "agent-user";
        SeedTickets(dbContext, ticket);
        var aiService = new StubAiTicketAssistantService();
        var service = CreateService(
            dbContext,
            new StubCurrentUserService(isAuthenticated: true, userId: "agent-user", role: AuthRoles.Agent),
            aiTicketAssistantService: aiService);

        var response = await service.SummarizeAsync(ticket.Id, new SummarizeTicketRequest(IncludeInternalNotes: true));

        Assert.Null(response);
        Assert.Null(aiService.LastSummaryRequest);
    }

    [Fact]
    public async Task SummarizeAsync_WhenAgentIsAssigned_ReturnsSummary()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Assigned agent summary");
        ticket.AssignedToUserId = "agent-user";
        SeedTickets(dbContext, ticket);
        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "agent-user",
            role: AuthRoles.Agent));

        var response = await service.SummarizeAsync(ticket.Id, new SummarizeTicketRequest());

        Assert.NotNull(response);
        Assert.Equal("Operational ticket summary.", response.Summary);
    }

    [Fact]
    public async Task SummarizeAsync_WhenTicketIsInaccessible_ReturnsNullWithoutCallingAi()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Inaccessible summary");
        ticket.AssignedToUserId = "other-agent";
        SeedTickets(dbContext, ticket);
        var aiService = new StubAiTicketAssistantService();
        var service = CreateService(
            dbContext,
            new StubCurrentUserService(isAuthenticated: true, userId: "agent-user", role: AuthRoles.Agent),
            aiTicketAssistantService: aiService);

        var response = await service.SummarizeAsync(ticket.Id, new SummarizeTicketRequest());

        Assert.Null(response);
        Assert.Null(aiService.LastSummaryRequest);
    }

    [Fact]
    public async Task SuggestTriageAsync_WhenAdmin_PassesVisibleContextAndMakesNoChanges()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket(
            "Payment triage",
            status: TicketStatus.InProgress,
            priority: TicketPriority.Medium,
            category: TicketCategory.Billing,
            description: "Customer cannot complete checkout.");
        ticket.AssignedToUserId = "agent-user";
        ticket.CustomerUserId = "customer-user";
        var newerMessage = CreateMessage(ticket.Id, "Second visible message", DateTime.UtcNow.AddMinutes(-1));
        newerMessage.CreatedByUserId = "agent-user";
        newerMessage.CreatedByDisplayName = "Support Agent";
        var olderMessage = CreateMessage(ticket.Id, "First visible message", DateTime.UtcNow.AddMinutes(-3));
        olderMessage.CreatedByUserId = "customer-user";
        olderMessage.CreatedByDisplayName = "Sara Ahmed";
        var internalNote = CreateMessage(ticket.Id, "Private payment processor note", DateTime.UtcNow.AddMinutes(-2), isInternalNote: true);
        internalNote.CreatedByUserId = "agent-user";
        internalNote.CreatedByDisplayName = "Support Agent";
        ticket.Messages.Add(newerMessage);
        ticket.Messages.Add(olderMessage);
        ticket.Messages.Add(internalNote);
        SeedTickets(dbContext, ticket);
        var aiService = new StubAiTicketAssistantService();
        var notificationService = new StubNotificationService();
        var service = CreateService(
            dbContext,
            new StubCurrentUserService(isAuthenticated: true, userId: "admin-user", role: AuthRoles.Admin),
            notificationService,
            aiService);

        var response = await service.SuggestTriageAsync(ticket.Id, new SuggestTriageRequest("  Focus on escalation.  "));

        Assert.NotNull(response);
        Assert.Equal(TicketPriority.Medium, response.CurrentPriority);
        Assert.Equal(TicketPriority.High, response.SuggestedPriority);
        Assert.Equal(TicketCategory.Billing, response.SuggestedCategory);
        Assert.True(response.EscalationRecommended);
        Assert.False(string.IsNullOrWhiteSpace(response.Rationale));
        var aiRequest = Assert.IsType<TicketTriageRequest>(aiService.LastTriageRequest);
        Assert.Equal("Payment triage", aiRequest.Title);
        Assert.Equal("Customer cannot complete checkout.", aiRequest.Description);
        Assert.Equal(TicketStatus.InProgress, aiRequest.Status);
        Assert.Equal(TicketPriority.Medium, aiRequest.CurrentPriority);
        Assert.Equal(TicketCategory.Billing, aiRequest.CurrentCategory);
        Assert.Equal("agent-user", aiRequest.AssignedAgentUserId);
        Assert.Equal("Support Agent", aiRequest.AssignedAgentDisplayName);
        Assert.Equal("Focus on escalation.", aiRequest.Instruction);
        Assert.Equal(["First visible message", "Second visible message"], aiRequest.Messages.Select(message => message.Body));
        Assert.Equal([AuthRoles.Customer, AuthRoles.Agent], aiRequest.Messages.Select(message => message.SenderRole));
        Assert.Equal(["Sara Ahmed", "Support Agent"], aiRequest.Messages.Select(message => message.SenderDisplayName));
        Assert.All(aiRequest.Messages, message => Assert.Equal(TimeSpan.Zero, message.CreatedAtUtc.Offset));
        Assert.DoesNotContain(aiRequest.Messages, message => message.Body == "Private payment processor note");
        Assert.Empty(notificationService.Requests);
        Assert.Empty(dbContext.AuditLogs);
        Assert.Equal(3, await dbContext.TicketMessages.CountAsync());
        var savedTicket = await dbContext.Tickets.SingleAsync();
        Assert.Equal(TicketStatus.InProgress, savedTicket.Status);
        Assert.Equal(TicketPriority.Medium, savedTicket.Priority);
        Assert.Equal(TicketCategory.Billing, savedTicket.Category);
        Assert.Equal("agent-user", savedTicket.AssignedToUserId);
        Assert.Equal("customer-user", savedTicket.CustomerUserId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SuggestTriageAsync_WhenInstructionIsEmpty_PassesNull(string? instruction)
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Instruction triage");
        SeedTickets(dbContext, ticket);
        var aiService = new StubAiTicketAssistantService();
        var service = CreateService(
            dbContext,
            new StubCurrentUserService(isAuthenticated: true, userId: "admin-user", role: AuthRoles.Admin),
            aiTicketAssistantService: aiService);

        var response = await service.SuggestTriageAsync(ticket.Id, new SuggestTriageRequest(instruction));

        Assert.NotNull(response);
        Assert.Null(aiService.LastTriageRequest?.Instruction);
    }

    [Fact]
    public async Task SuggestTriageAsync_WhenInstructionIsOverlong_ThrowsValidationException()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateAdminService(dbContext);

        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            service.SuggestTriageAsync(Guid.NewGuid(), new SuggestTriageRequest(new string('x', 501))));
    }

    [Fact]
    public async Task SuggestTriageAsync_WhenAgentIsAssigned_ReturnsSuggestion()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Assigned agent triage");
        ticket.AssignedToUserId = "agent-user";
        SeedTickets(dbContext, ticket);
        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "agent-user",
            role: AuthRoles.Agent));

        var response = await service.SuggestTriageAsync(ticket.Id, new SuggestTriageRequest());

        Assert.NotNull(response);
        Assert.Equal(TicketPriority.High, response.SuggestedPriority);
    }

    [Fact]
    public async Task SuggestTriageAsync_WhenTicketIsInaccessible_ReturnsNullWithoutCallingAi()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Inaccessible triage");
        ticket.AssignedToUserId = "other-agent";
        SeedTickets(dbContext, ticket);
        var aiService = new StubAiTicketAssistantService();
        var service = CreateService(
            dbContext,
            new StubCurrentUserService(isAuthenticated: true, userId: "agent-user", role: AuthRoles.Agent),
            aiTicketAssistantService: aiService);

        var response = await service.SuggestTriageAsync(ticket.Id, new SuggestTriageRequest());

        Assert.Null(response);
        Assert.Null(aiService.LastTriageRequest);
    }

    [Fact]
    public async Task AddMessageAsync_WhenMessageIsEmpty_ThrowsValidationException()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Ticket with invalid reply");
        SeedTickets(dbContext, ticket);

        var service = CreateService(dbContext);

        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            service.AddMessageAsync(ticket.Id, new AddTicketMessageRequest("", false, null, null)));
    }

    [Fact]
    public async Task AddMessageAsync_WhenTicketDoesNotExist_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();
        var notificationService = new StubNotificationService();
        var service = CreateService(dbContext, notificationService: notificationService);

        var response = await service.AddMessageAsync(Guid.NewGuid(), new AddTicketMessageRequest(
            "Message for missing ticket.",
            false,
            null,
            null));

        Assert.Null(response);
        Assert.Empty(notificationService.Requests);
    }

    [Fact]
    public async Task AddMessageAsync_WhenCustomerAddsMessage_NotifiesAssignedAgent()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Customer to agent message");
        ticket.CustomerUserId = "customer-user";
        ticket.AssignedToUserId = "agent-user";
        SeedTickets(dbContext, ticket);
        var notificationService = new StubNotificationService();
        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "customer-user",
            role: AuthRoles.Customer), notificationService);

        await service.AddMessageAsync(ticket.Id, new AddTicketMessageRequest(
            "Customer reply.",
            false,
            null,
            null));

        var notification = Assert.Single(notificationService.Requests);
        Assert.Equal("agent-user", notification.UserId);
        Assert.Equal("TicketMessageCreated", notification.Type);
        Assert.Equal(ticket.Id, notification.TicketId);
        Assert.Equal("New ticket message", notification.Title);
        Assert.Equal("A new message was added to ticket 'Customer to agent message'.", notification.Message);
    }

    [Fact]
    public async Task AddMessageAsync_WhenAgentAddsMessage_NotifiesCustomer()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Agent to customer message");
        ticket.AssignedToUserId = "agent-user";
        ticket.CustomerUserId = "customer-user";
        SeedTickets(dbContext, ticket);
        var notificationService = new StubNotificationService();
        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "agent-user",
            role: AuthRoles.Agent), notificationService);

        await service.AddMessageAsync(ticket.Id, new AddTicketMessageRequest(
            "Agent reply.",
            false,
            null,
            null));

        Assert.Equal("customer-user", Assert.Single(notificationService.Requests).UserId);
    }

    [Fact]
    public async Task AddMessageAsync_WhenAdminAddsMessage_NotifiesCustomer()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Admin to customer message");
        ticket.CustomerUserId = "customer-user";
        SeedTickets(dbContext, ticket);
        var notificationService = new StubNotificationService();
        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "admin-user",
            role: AuthRoles.Admin), notificationService);

        await service.AddMessageAsync(ticket.Id, new AddTicketMessageRequest(
            "Admin reply.",
            false,
            null,
            null));

        Assert.Equal("customer-user", Assert.Single(notificationService.Requests).UserId);
    }

    [Fact]
    public async Task AddMessageAsync_WhenThereIsNoValidRecipient_DoesNotCreateNotification()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Message without recipient");
        SeedTickets(dbContext, ticket);
        var notificationService = new StubNotificationService();
        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "admin-user",
            role: AuthRoles.Admin), notificationService);

        var response = await service.AddMessageAsync(ticket.Id, new AddTicketMessageRequest(
            "Admin reply.",
            false,
            null,
            null));

        Assert.NotNull(response);
        Assert.Empty(notificationService.Requests);
    }

    [Fact]
    public async Task AddMessageAsync_WhenRecipientIsSender_DoesNotCreateNotification()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Self notification ticket");
        ticket.AssignedToUserId = "customer-user";
        ticket.CustomerUserId = "customer-user";
        SeedTickets(dbContext, ticket);
        var notificationService = new StubNotificationService();
        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "customer-user",
            role: AuthRoles.Customer), notificationService);

        await service.AddMessageAsync(ticket.Id, new AddTicketMessageRequest(
            "Customer reply.",
            false,
            null,
            null));

        Assert.Empty(notificationService.Requests);
    }

    [Fact]
    public async Task AddMessageAsync_WhenTicketIsDeleted_DoesNotCreateNotification()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Deleted message ticket", isDeleted: true);
        ticket.CustomerUserId = "customer-user";
        ticket.AssignedToUserId = "agent-user";
        SeedTickets(dbContext, ticket);
        var notificationService = new StubNotificationService();
        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "customer-user",
            role: AuthRoles.Customer), notificationService);

        var response = await service.AddMessageAsync(ticket.Id, new AddTicketMessageRequest(
            "Customer reply.",
            false,
            null,
            null));

        Assert.Null(response);
        Assert.Empty(notificationService.Requests);
    }

    [Fact]
    public async Task AddMessageAsync_WhenAdmin_AddsMessageToAnyTicket()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Admin message ticket");
        ticket.CustomerUserId = "customer-user";
        SeedTickets(dbContext, ticket);

        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "admin-user",
            email: "admin@example.com",
            fullName: "Admin User",
            role: AuthRoles.Admin));

        var response = await service.AddMessageAsync(ticket.Id, new AddTicketMessageRequest(
            "Admin reply.",
            false,
            "request-user",
            "Request User"));

        Assert.NotNull(response);
        Assert.Equal("admin-user", response.Message.CreatedByUserId);
    }

    [Fact]
    public async Task AddMessageAsync_WhenAgentIsAssigned_AddsMessage()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Assigned agent message ticket");
        ticket.AssignedToUserId = "agent-user";
        SeedTickets(dbContext, ticket);

        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "agent-user",
            email: "agent@example.com",
            fullName: "Agent User",
            role: AuthRoles.Agent));

        var response = await service.AddMessageAsync(ticket.Id, new AddTicketMessageRequest(
            "Agent reply.",
            true,
            null,
            null));

        Assert.NotNull(response);
        Assert.True(response.Message.IsInternalNote);
    }

    [Fact]
    public async Task AddMessageAsync_WhenAgentIsNotAssigned_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Unassigned agent message ticket");
        ticket.AssignedToUserId = "other-agent";
        SeedTickets(dbContext, ticket);

        ticket.CustomerUserId = "customer-user";
        var notificationService = new StubNotificationService();
        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "agent-user",
            email: "agent@example.com",
            fullName: "Agent User",
            role: AuthRoles.Agent), notificationService);

        var response = await service.AddMessageAsync(ticket.Id, new AddTicketMessageRequest(
            "Agent reply.",
            false,
            null,
            null));

        Assert.Null(response);
        Assert.Empty(dbContext.TicketMessages);
        Assert.Empty(notificationService.Requests);
    }

    [Fact]
    public async Task AddMessageAsync_WhenCustomerOwnsTicket_AddsMessage()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Owned customer message ticket");
        ticket.CustomerUserId = "customer-user";
        SeedTickets(dbContext, ticket);

        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "customer-user",
            email: "customer@example.com",
            fullName: "Customer User",
            role: AuthRoles.Customer));

        var response = await service.AddMessageAsync(ticket.Id, new AddTicketMessageRequest(
            "Customer reply.",
            false,
            null,
            null));

        Assert.NotNull(response);
        Assert.False(response.Message.IsInternalNote);
        Assert.Equal("customer-user", response.Message.CreatedByUserId);
    }

    [Fact]
    public async Task AddMessageAsync_WhenCustomerDoesNotOwnTicket_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Other customer message ticket");
        ticket.CustomerUserId = "other-customer";
        SeedTickets(dbContext, ticket);

        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "customer-user",
            email: "customer@example.com",
            fullName: "Customer User",
            role: AuthRoles.Customer));

        var response = await service.AddMessageAsync(ticket.Id, new AddTicketMessageRequest(
            "Customer reply.",
            false,
            null,
            null));

        Assert.Null(response);
        Assert.Empty(dbContext.TicketMessages);
    }

    [Fact]
    public async Task AddMessageAsync_WhenCustomerAddsInternalNote_ThrowsValidationException()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Customer internal note ticket");
        ticket.CustomerUserId = "customer-user";
        SeedTickets(dbContext, ticket);

        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "customer-user",
            email: "customer@example.com",
            fullName: "Customer User",
            role: AuthRoles.Customer));

        var exception = await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            service.AddMessageAsync(ticket.Id, new AddTicketMessageRequest(
                "Customer internal note.",
                true,
                null,
                null)));

        Assert.Contains(exception.Errors, error => error.ErrorMessage == "Customers cannot add internal notes.");
    }

    [Fact]
    public async Task ChangeStatusAsync_ChangesStatusToInProgress()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Ticket to start");
        SeedTickets(dbContext, ticket);

        var service = CreateAdminService(dbContext);

        var response = await service.ChangeStatusAsync(ticket.Id, new ChangeTicketStatusRequest(
            TicketStatus.InProgress,
            "user-1",
            "Support Agent"));

        var savedTicket = await dbContext.Tickets.SingleAsync();

        Assert.NotNull(response);
        Assert.Equal(TicketStatus.InProgress, response.Ticket.Status);
        Assert.Equal(TicketStatus.InProgress, savedTicket.Status);
        Assert.NotNull(savedTicket.UpdatedAtUtc);
    }

    [Fact]
    public async Task ChangeStatusAsync_WhenResolved_SetsResolvedAtUtc()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Ticket to resolve");
        SeedTickets(dbContext, ticket);

        var service = CreateAdminService(dbContext);

        var response = await service.ChangeStatusAsync(ticket.Id, new ChangeTicketStatusRequest(TicketStatus.Resolved, null, null));

        var savedTicket = await dbContext.Tickets.SingleAsync();

        Assert.Equal(TicketStatus.Resolved, savedTicket.Status);
        Assert.NotNull(savedTicket.ResolvedAtUtc);
        Assert.NotNull(response);
        Assert.NotNull(response.Ticket.ResolvedAtUtc);
        Assert.Equal(TimeSpan.Zero, response.Ticket.ResolvedAtUtc.Value.Offset);
    }

    [Fact]
    public async Task ChangeStatusAsync_WhenClosed_SetsClosedAtUtc()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Ticket to close");
        SeedTickets(dbContext, ticket);

        var service = CreateAdminService(dbContext);

        var response = await service.ChangeStatusAsync(ticket.Id, new ChangeTicketStatusRequest(TicketStatus.Closed, null, null));

        var savedTicket = await dbContext.Tickets.SingleAsync();

        Assert.Equal(TicketStatus.Closed, savedTicket.Status);
        Assert.NotNull(savedTicket.ClosedAtUtc);
        Assert.NotNull(response);
        Assert.NotNull(response.Ticket.ClosedAtUtc);
        Assert.Equal(TimeSpan.Zero, response.Ticket.ClosedAtUtc.Value.Offset);
    }

    [Fact]
    public async Task ChangeStatusAsync_WhenTicketDoesNotExist_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();
        var notificationService = new StubNotificationService();
        var service = CreateService(dbContext, notificationService: notificationService);

        var response = await service.ChangeStatusAsync(Guid.NewGuid(), new ChangeTicketStatusRequest(
            TicketStatus.InProgress,
            null,
            null));

        Assert.Null(response);
        Assert.Empty(notificationService.Requests);
    }

    [Fact]
    public async Task ChangeStatusAsync_CreatesStoredNotificationForCustomerUserId()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Customer status notification", status: TicketStatus.Open);
        ticket.CustomerUserId = "customer-user";
        SeedTickets(dbContext, ticket);
        var notificationService = new NotificationService(
            dbContext,
            new StubCurrentUserService(),
            new NoOpHubContext(),
            NullLogger<NotificationService>.Instance);
        var service = CreateService(
            dbContext,
            new StubCurrentUserService(
                isAuthenticated: true,
                userId: "admin-user",
                role: AuthRoles.Admin),
            notificationService);

        await service.ChangeStatusAsync(ticket.Id, new ChangeTicketStatusRequest(
            TicketStatus.InProgress,
            null,
            null));

        var notification = await dbContext.Notifications.SingleAsync();

        Assert.Equal("customer-user", notification.UserId);
        Assert.Equal("TicketStatusChanged", notification.Type);
        Assert.Equal(ticket.Id, notification.TicketId);
        Assert.Equal("Ticket status updated", notification.Title);
        Assert.Equal(
            "Your ticket 'Customer status notification' status was updated to InProgress.",
            notification.Message);
    }

    [Fact]
    public async Task ChangeStatusAsync_WhenCustomerUserIdIsNull_DoesNotCreateNotification()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Status without customer", status: TicketStatus.Open);
        SeedTickets(dbContext, ticket);
        var notificationService = new StubNotificationService();
        var service = CreateService(
            dbContext,
            new StubCurrentUserService(isAuthenticated: true, userId: "admin-user", role: AuthRoles.Admin),
            notificationService);

        await service.ChangeStatusAsync(ticket.Id, new ChangeTicketStatusRequest(
            TicketStatus.InProgress,
            null,
            null));

        Assert.Empty(notificationService.Requests);
    }

    [Fact]
    public async Task ChangeStatusAsync_WhenStatusIsUnchanged_UpdatesNothingAndDoesNotCreateNotification()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Unchanged status ticket", status: TicketStatus.Open);
        ticket.CustomerUserId = "customer-user";
        SeedTickets(dbContext, ticket);
        var notificationService = new StubNotificationService();
        var service = CreateService(
            dbContext,
            new StubCurrentUserService(isAuthenticated: true, userId: "admin-user", role: AuthRoles.Admin),
            notificationService);

        var response = await service.ChangeStatusAsync(ticket.Id, new ChangeTicketStatusRequest(
            TicketStatus.Open,
            null,
            null));

        var savedTicket = await dbContext.Tickets.SingleAsync();
        Assert.NotNull(response);
        Assert.Null(savedTicket.UpdatedAtUtc);
        Assert.Empty(dbContext.AuditLogs);
        Assert.Empty(notificationService.Requests);
    }

    [Fact]
    public async Task ChangeStatusAsync_WhenTicketIsDeleted_DoesNotCreateNotification()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Deleted status ticket", status: TicketStatus.Open, isDeleted: true);
        ticket.CustomerUserId = "customer-user";
        SeedTickets(dbContext, ticket);
        var notificationService = new StubNotificationService();
        var service = CreateService(
            dbContext,
            new StubCurrentUserService(isAuthenticated: true, userId: "admin-user", role: AuthRoles.Admin),
            notificationService);

        var response = await service.ChangeStatusAsync(ticket.Id, new ChangeTicketStatusRequest(
            TicketStatus.InProgress,
            null,
            null));

        Assert.Null(response);
        Assert.Empty(notificationService.Requests);
    }

    [Fact]
    public async Task ChangeStatusAsync_CreatesAuditLogWithOldAndNewStatus()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Ticket audit", status: TicketStatus.Open);
        SeedTickets(dbContext, ticket);

        var service = CreateAdminService(dbContext);

        await service.ChangeStatusAsync(ticket.Id, new ChangeTicketStatusRequest(
            TicketStatus.InProgress,
            "user-1",
            "Support Agent"));

        var auditLog = await dbContext.AuditLogs.SingleAsync();

        Assert.Equal(nameof(Ticket), auditLog.EntityName);
        Assert.Equal(ticket.Id, auditLog.EntityId);
        Assert.Equal("StatusChanged", auditLog.Action);
        Assert.Equal("""{"status":"Open"}""", auditLog.OldValues);
        Assert.Equal("""{"status":"InProgress"}""", auditLog.NewValues);
        Assert.Equal("admin-user", auditLog.PerformedByUserId);
        Assert.Equal("Admin User", auditLog.PerformedByDisplayName);
    }

    [Fact]
    public async Task ChangeStatusAsync_WhenUserIsAuthenticated_UsesCurrentUserForAuditFields()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Ticket authenticated status audit", status: TicketStatus.Open);
        ticket.AssignedToUserId = "authenticated-user";
        SeedTickets(dbContext, ticket);

        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "authenticated-user",
            email: "agent@example.com",
            fullName: "Authenticated Agent",
            role: "Agent"));

        await service.ChangeStatusAsync(ticket.Id, new ChangeTicketStatusRequest(
            TicketStatus.InProgress,
            "request-user",
            "Request User"));

        var auditLog = await dbContext.AuditLogs.SingleAsync();

        Assert.Equal("authenticated-user", auditLog.PerformedByUserId);
        Assert.Equal("Authenticated Agent", auditLog.PerformedByDisplayName);
    }

    [Fact]
    public async Task ChangeStatusAsync_WhenAgentIsAssigned_ChangesStatus()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Assigned status ticket", status: TicketStatus.Open);
        ticket.AssignedToUserId = "agent-user";
        SeedTickets(dbContext, ticket);

        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "agent-user",
            email: "agent@example.com",
            fullName: "Agent User",
            role: AuthRoles.Agent));

        var response = await service.ChangeStatusAsync(ticket.Id, new ChangeTicketStatusRequest(
            TicketStatus.InProgress,
            null,
            null));

        Assert.NotNull(response);
        Assert.Equal(TicketStatus.InProgress, response.Ticket.Status);
    }

    [Fact]
    public async Task ChangeStatusAsync_WhenAgentIsNotAssigned_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Unassigned status ticket", status: TicketStatus.Open);
        ticket.AssignedToUserId = "other-agent";
        ticket.CustomerUserId = "customer-user";
        SeedTickets(dbContext, ticket);

        var notificationService = new StubNotificationService();
        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "agent-user",
            email: "agent@example.com",
            fullName: "Agent User",
            role: AuthRoles.Agent), notificationService);

        var response = await service.ChangeStatusAsync(ticket.Id, new ChangeTicketStatusRequest(
            TicketStatus.InProgress,
            null,
            null));

        var savedTicket = await dbContext.Tickets.SingleAsync();

        Assert.Null(response);
        Assert.Equal(TicketStatus.Open, savedTicket.Status);
        Assert.Empty(dbContext.AuditLogs);
        Assert.Empty(notificationService.Requests);
    }

    [Fact]
    public async Task ChangeStatusAsync_WhenUserIsUnauthenticated_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Ticket anonymous status audit", status: TicketStatus.Open);
        SeedTickets(dbContext, ticket);

        var service = CreateService(dbContext, new StubCurrentUserService());

        var response = await service.ChangeStatusAsync(ticket.Id, new ChangeTicketStatusRequest(
            TicketStatus.InProgress,
            "request-user",
            "Request User"));

        Assert.Null(response);
        Assert.Empty(dbContext.AuditLogs);
    }

    [Fact]
    public async Task ChangeStatusAsync_WhenStatusIsInvalid_ThrowsValidationException()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Ticket invalid status");
        SeedTickets(dbContext, ticket);

        var service = CreateAdminService(dbContext);

        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            service.ChangeStatusAsync(ticket.Id, new ChangeTicketStatusRequest((TicketStatus)999, null, null)));
    }

    [Fact]
    public async Task AssignAsync_UpdatesAssignedToUserIdAndUpdatedAtUtc()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Ticket to assign");
        SeedTickets(dbContext, ticket);

        var service = CreateService(dbContext);

        var response = await service.AssignAsync(ticket.Id, new AssignTicketRequest(Guid.Parse(AgentUserId)));

        var savedTicket = await dbContext.Tickets.SingleAsync();

        Assert.NotNull(response);
        Assert.Equal(AgentUserId, response.Ticket.AssignedToUserId);
        Assert.Equal(ticket.Id, response.TicketId);
        Assert.Equal(AgentUserId, response.AssignedToUserId);
        Assert.Equal("Assigned Agent", response.AssignedToDisplayName);
        Assert.Equal(AgentUserId, savedTicket.AssignedToUserId);
        Assert.NotNull(savedTicket.UpdatedAtUtc);
    }

    [Fact]
    public async Task AssignAsync_WhenUserIsAuthenticated_UsesCurrentUserForAuditFields()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Ticket authenticated assignment audit");
        SeedTickets(dbContext, ticket);

        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "authenticated-manager",
            email: "manager@example.com",
            fullName: "Authenticated Manager",
            role: "Admin"));

        await service.AssignAsync(ticket.Id, new AssignTicketRequest(Guid.Parse(AgentUserId)));

        var auditLog = await dbContext.AuditLogs.SingleAsync();

        Assert.Equal("authenticated-manager", auditLog.PerformedByUserId);
        Assert.Equal("Authenticated Manager", auditLog.PerformedByDisplayName);
    }

    [Fact]
    public async Task AssignAsync_WhenUserIsUnauthenticated_LeavesAuditActorEmpty()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Ticket anonymous assignment audit");
        SeedTickets(dbContext, ticket);

        var service = CreateService(dbContext, new StubCurrentUserService());

        await service.AssignAsync(ticket.Id, new AssignTicketRequest(Guid.Parse(AgentUserId)));

        var auditLog = await dbContext.AuditLogs.SingleAsync();

        Assert.Null(auditLog.PerformedByUserId);
        Assert.Null(auditLog.PerformedByDisplayName);
    }

    [Fact]
    public async Task AssignAsync_WhenUserIsAuthenticated_AssignedToUserIdStillComesFromRequestOnly()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Ticket authenticated assignment target");
        SeedTickets(dbContext, ticket);

        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "authenticated-manager",
            email: "manager@example.com",
            fullName: "Authenticated Manager",
            role: "Admin"));

        var response = await service.AssignAsync(ticket.Id, new AssignTicketRequest(Guid.Parse(NewAgentUserId)));

        var savedTicket = await dbContext.Tickets.SingleAsync();

        Assert.NotNull(response);
        Assert.Equal(NewAgentUserId, response.Ticket.AssignedToUserId);
        Assert.Equal(NewAgentUserId, savedTicket.AssignedToUserId);
    }

    [Fact]
    public async Task AssignAsync_WhenTicketIsOpen_ChangesStatusToInProgress()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Open ticket to assign", status: TicketStatus.Open);
        SeedTickets(dbContext, ticket);

        var service = CreateService(dbContext);

        var response = await service.AssignAsync(ticket.Id, new AssignTicketRequest(Guid.Parse(AgentUserId)));

        Assert.NotNull(response);
        Assert.Equal(TicketStatus.InProgress, response.Ticket.Status);
    }

    [Fact]
    public async Task AssignAsync_WhenTicketIsNotOpen_DoesNotOverwriteStatus()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Resolved ticket to assign", status: TicketStatus.Resolved);
        SeedTickets(dbContext, ticket);

        var service = CreateService(dbContext);

        var response = await service.AssignAsync(ticket.Id, new AssignTicketRequest(Guid.Parse(AgentUserId)));

        Assert.NotNull(response);
        Assert.Equal(TicketStatus.Resolved, response.Ticket.Status);
    }

    [Fact]
    public async Task AssignAsync_WhenTicketDoesNotExist_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var response = await service.AssignAsync(Guid.NewGuid(), new AssignTicketRequest(Guid.Parse(AgentUserId)));

        Assert.Null(response);
    }

    [Fact]
    public async Task AssignAsync_WhenAssignedToUserIdIsEmpty_ThrowsValidationException()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Ticket invalid assignment");
        SeedTickets(dbContext, ticket);

        var service = CreateService(dbContext);

        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            service.AssignAsync(ticket.Id, new AssignTicketRequest(Guid.Empty)));
    }

    [Theory]
    [InlineData("44444444-4444-4444-4444-444444444444")]
    [InlineData(CustomerUserId)]
    public async Task AssignAsync_WhenAssignedToUserIdIsNotAnActiveAgent_ThrowsValidationExceptionAndDoesNotPersistPartialData(
        string assignedToUserId)
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Ticket invalid assignment target", status: TicketStatus.Open);
        SeedTickets(dbContext, ticket);
        var notificationService = new StubNotificationService();
        var service = CreateService(
            dbContext,
            notificationService: notificationService,
            userLookupService: new StubUserLookupService(assignedToUserId));

        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            service.AssignAsync(ticket.Id, new AssignTicketRequest(Guid.Parse(assignedToUserId))));

        var savedTicket = await dbContext.Tickets.SingleAsync();
        Assert.Null(savedTicket.AssignedToUserId);
        Assert.Equal(TicketStatus.Open, savedTicket.Status);
        Assert.Empty(dbContext.AuditLogs);
        Assert.Empty(notificationService.Requests);
    }

    [Fact]
    public async Task AssignAsync_CreatesAuditLogWithOldAndNewAssignmentAndStatus()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Ticket assignment audit", status: TicketStatus.Open);
        ticket.AssignedToUserId = "old-agent";
        SeedTickets(dbContext, ticket);

        var service = CreateService(dbContext);

        await service.AssignAsync(ticket.Id, new AssignTicketRequest(Guid.Parse(NewAgentUserId)));

        var auditLog = await dbContext.AuditLogs.SingleAsync();

        Assert.Equal(nameof(Ticket), auditLog.EntityName);
        Assert.Equal(ticket.Id, auditLog.EntityId);
        Assert.Equal("TicketAssigned", auditLog.Action);
        Assert.Equal("""{"assignedToUserId":"old-agent","status":"Open"}""", auditLog.OldValues);
        Assert.Equal($$"""{"assignedToUserId":"{{NewAgentUserId}}","status":"InProgress"}""", auditLog.NewValues);
        Assert.Null(auditLog.PerformedByUserId);
        Assert.Null(auditLog.PerformedByDisplayName);
    }

    [Fact]
    public async Task AssignAsync_CreatesStoredNotificationForAssignedUser()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Notification assignment ticket", status: TicketStatus.Open);
        SeedTickets(dbContext, ticket);
        var notificationService = new NotificationService(
            dbContext,
            new StubCurrentUserService(),
            new NoOpHubContext(),
            NullLogger<NotificationService>.Instance);
        var service = CreateService(dbContext, notificationService: notificationService);

        await service.AssignAsync(ticket.Id, new AssignTicketRequest(Guid.Parse(AgentUserId)));

        var notification = await dbContext.Notifications.SingleAsync();

        Assert.Equal(AgentUserId, notification.UserId);
        Assert.Equal("TicketAssigned", notification.Type);
        Assert.Equal(ticket.Id, notification.TicketId);
        Assert.Equal("Ticket assigned", notification.Title);
        Assert.Equal("Ticket 'Notification assignment ticket' has been assigned to you.", notification.Message);
    }

    [Fact]
    public async Task AssignAsync_WhenTicketDoesNotExist_DoesNotCreateNotification()
    {
        await using var dbContext = CreateDbContext();
        var notificationService = new StubNotificationService();
        var service = CreateService(dbContext, notificationService: notificationService);

        var response = await service.AssignAsync(Guid.NewGuid(), new AssignTicketRequest(Guid.Parse(AgentUserId)));

        Assert.Null(response);
        Assert.Empty(notificationService.Requests);
    }

    [Fact]
    public async Task AssignAsync_WhenTicketIsDeleted_DoesNotCreateNotification()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Deleted assignment ticket", isDeleted: true);
        SeedTickets(dbContext, ticket);
        var notificationService = new StubNotificationService();
        var service = CreateService(dbContext, notificationService: notificationService);

        var response = await service.AssignAsync(ticket.Id, new AssignTicketRequest(Guid.Parse(AgentUserId)));

        Assert.Null(response);
        Assert.Empty(notificationService.Requests);
    }

    [Fact]
    public async Task GetAuditLogsAsync_ReturnsTicketCreationStatusAndAssignmentLogs()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Ticket with audit logs");
        SeedTickets(dbContext, ticket);
        SeedAuditLogs(dbContext,
            CreateAuditLog(nameof(Ticket), ticket.Id, "TicketCreated", DateTime.UtcNow.AddMinutes(1)),
            CreateAuditLog(nameof(Ticket), ticket.Id, "StatusChanged", DateTime.UtcNow.AddMinutes(2)),
            CreateAuditLog(nameof(Ticket), ticket.Id, "TicketAssigned", DateTime.UtcNow.AddMinutes(3)));

        var service = CreateService(dbContext);

        var result = await service.GetAuditLogsAsync(ticket.Id);

        Assert.NotNull(result);
        Assert.Collection(
            result,
            auditLog => Assert.Equal("TicketCreated", auditLog.Action),
            auditLog => Assert.Equal("StatusChanged", auditLog.Action),
            auditLog => Assert.Equal("TicketAssigned", auditLog.Action));
    }

    [Fact]
    public async Task GetAuditLogsAsync_IncludesTicketMessageAuditLogsForTicket()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Ticket with message audit logs");
        var message = CreateMessage(ticket.Id, "Message", DateTime.UtcNow);
        ticket.Messages.Add(message);
        SeedTickets(dbContext, ticket);
        SeedAuditLogs(dbContext,
            CreateAuditLog(nameof(Ticket), ticket.Id, "TicketCreated", DateTime.UtcNow.AddMinutes(1)),
            CreateAuditLog(nameof(TicketMessage), message.Id, "MessageAdded", DateTime.UtcNow.AddMinutes(2)));

        var service = CreateService(dbContext);

        var result = await service.GetAuditLogsAsync(ticket.Id);

        Assert.NotNull(result);
        Assert.Contains(result, auditLog => auditLog.Action == "MessageAdded");
    }

    [Fact]
    public async Task GetAuditLogsAsync_OrdersLogsByPerformedAtUtcAscending()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Ticket with ordered audit logs");
        SeedTickets(dbContext, ticket);
        SeedAuditLogs(dbContext,
            CreateAuditLog(nameof(Ticket), ticket.Id, "Newest", DateTime.UtcNow.AddMinutes(3)),
            CreateAuditLog(nameof(Ticket), ticket.Id, "Oldest", DateTime.UtcNow.AddMinutes(1)),
            CreateAuditLog(nameof(Ticket), ticket.Id, "Middle", DateTime.UtcNow.AddMinutes(2)));

        var service = CreateService(dbContext);

        var result = await service.GetAuditLogsAsync(ticket.Id);

        Assert.NotNull(result);
        Assert.Collection(
            result,
            auditLog => Assert.Equal("Oldest", auditLog.Action),
            auditLog => Assert.Equal("Middle", auditLog.Action),
            auditLog => Assert.Equal("Newest", auditLog.Action));
        Assert.All(result, auditLog => Assert.Equal(TimeSpan.Zero, auditLog.PerformedAtUtc.Offset));
    }

    [Fact]
    public async Task GetAuditLogsAsync_WhenTicketDoesNotExist_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var result = await service.GetAuditLogsAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAuditLogsAsync_ExcludesLogsFromOtherTickets()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Target ticket");
        var otherTicket = CreateTicket("Other ticket");
        SeedTickets(dbContext, ticket, otherTicket);
        SeedAuditLogs(dbContext,
            CreateAuditLog(nameof(Ticket), ticket.Id, "TargetCreated", DateTime.UtcNow.AddMinutes(1)),
            CreateAuditLog(nameof(Ticket), otherTicket.Id, "OtherCreated", DateTime.UtcNow.AddMinutes(2)));

        var service = CreateService(dbContext);

        var result = await service.GetAuditLogsAsync(ticket.Id);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("TargetCreated", result[0].Action);
    }

    [Fact]
    public async Task DeleteAsync_SetsIsDeletedTrueAndUpdatesUpdatedAtUtc()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Ticket to delete");
        SeedTickets(dbContext, ticket);

        var service = CreateService(dbContext);

        var response = await service.DeleteAsync(ticket.Id, new DeleteTicketRequest(
            "user-1",
            "Support Manager",
            "Duplicate ticket"));

        var savedTicket = await dbContext.Tickets.SingleAsync();

        Assert.NotNull(response);
        Assert.Equal(ticket.Id, response.TicketId);
        Assert.True(response.IsDeleted);
        Assert.Equal(TimeSpan.Zero, response.DeletedAtUtc.Offset);
        Assert.True(savedTicket.IsDeleted);
        Assert.NotNull(savedTicket.UpdatedAtUtc);
    }

    [Fact]
    public async Task DeleteAsync_CreatesTicketDeletedAuditLog()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Ticket delete audit");
        SeedTickets(dbContext, ticket);

        var service = CreateService(dbContext);

        await service.DeleteAsync(ticket.Id, new DeleteTicketRequest(
            "user-1",
            "Support Manager",
            "Duplicate ticket"));

        var auditLog = await dbContext.AuditLogs.SingleAsync();

        Assert.Equal(nameof(Ticket), auditLog.EntityName);
        Assert.Equal(ticket.Id, auditLog.EntityId);
        Assert.Equal("TicketDeleted", auditLog.Action);
        Assert.Equal("""{"isDeleted":false}""", auditLog.OldValues);
        Assert.Equal("""{"isDeleted":true,"reason":"Duplicate ticket"}""", auditLog.NewValues);
        Assert.Equal("user-1", auditLog.PerformedByUserId);
        Assert.Equal("Support Manager", auditLog.PerformedByDisplayName);
    }

    [Fact]
    public async Task DeleteAsync_WhenUserIsAuthenticated_UsesCurrentUserForAuditFields()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Ticket authenticated delete audit");
        SeedTickets(dbContext, ticket);

        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "authenticated-manager",
            email: "manager@example.com",
            fullName: "Authenticated Manager",
            role: "Admin"));

        await service.DeleteAsync(ticket.Id, new DeleteTicketRequest(
            "request-manager",
            "Request Manager",
            "Duplicate ticket"));

        var auditLog = await dbContext.AuditLogs.SingleAsync();

        Assert.Equal("authenticated-manager", auditLog.PerformedByUserId);
        Assert.Equal("Authenticated Manager", auditLog.PerformedByDisplayName);
    }

    [Fact]
    public async Task DeleteAsync_WhenUserIsUnauthenticated_UsesRequestFieldsForAuditFields()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Ticket anonymous delete audit");
        SeedTickets(dbContext, ticket);

        var service = CreateService(dbContext, new StubCurrentUserService());

        await service.DeleteAsync(ticket.Id, new DeleteTicketRequest(
            "request-manager",
            "Request Manager",
            "Duplicate ticket"));

        var auditLog = await dbContext.AuditLogs.SingleAsync();

        Assert.Equal("request-manager", auditLog.PerformedByUserId);
        Assert.Equal("Request Manager", auditLog.PerformedByDisplayName);
    }

    [Fact]
    public async Task DeleteAsync_WhenTicketDoesNotExist_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var response = await service.DeleteAsync(Guid.NewGuid(), new DeleteTicketRequest(null, null, null));

        Assert.Null(response);
    }

    [Fact]
    public async Task DeleteAsync_WhenTicketIsAlreadyDeleted_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Already deleted ticket", isDeleted: true);
        SeedTickets(dbContext, ticket);

        var service = CreateService(dbContext);

        var response = await service.DeleteAsync(ticket.Id, new DeleteTicketRequest(null, null, null));

        Assert.Null(response);
    }

    [Fact]
    public async Task DeletedTicket_IsExcludedFromListAndGetById()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Deleted ticket", isDeleted: true);
        SeedTickets(dbContext, ticket);

        var service = CreateService(dbContext);

        var listResult = await service.GetListAsync(new GetTicketsQuery());
        var detailResult = await service.GetByIdAsync(ticket.Id);

        Assert.Empty(listResult.Items);
        Assert.Null(detailResult);
    }

    [Fact]
    public async Task DeletedTicket_CannotAddMessageChangeStatusOrAssign()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket("Deleted protected ticket", isDeleted: true);
        SeedTickets(dbContext, ticket);

        var service = CreateService(dbContext);

        var messageResponse = await service.AddMessageAsync(ticket.Id, new AddTicketMessageRequest(
            "Message",
            false,
            null,
            null));
        var statusResponse = await service.ChangeStatusAsync(ticket.Id, new ChangeTicketStatusRequest(
            TicketStatus.InProgress,
            null,
            null));
        var assignResponse = await service.AssignAsync(ticket.Id, new AssignTicketRequest(Guid.Parse(AgentUserId)));

        Assert.Null(messageResponse);
        Assert.Null(statusResponse);
        Assert.Null(assignResponse);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static TicketService CreateService(
        ApplicationDbContext dbContext,
        ICurrentUserService? currentUserService = null,
        INotificationService? notificationService = null,
        IAiTicketAssistantService? aiTicketAssistantService = null,
        IUserLookupService? userLookupService = null) =>
        new(
            dbContext,
            aiTicketAssistantService ?? new StubAiTicketAssistantService(),
            new CreateTicketRequestValidator(),
            new AddTicketMessageRequestValidator(),
            new AddInternalNoteRequestValidator(),
            new SuggestReplyRequestValidator(),
            new SuggestTriageRequestValidator(),
            new ChangeTicketStatusRequestValidator(),
            new AssignTicketRequestValidator(),
            new DeleteTicketRequestValidator(),
            currentUserService ?? new StubCurrentUserService(),
            notificationService ?? new StubNotificationService(),
            userLookupService ?? new StubUserLookupService());

    private static TicketService CreateAdminService(ApplicationDbContext dbContext) =>
        CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "admin-user",
            email: "admin@example.com",
            fullName: "Admin User",
            role: AuthRoles.Admin));

    private static void SeedTickets(ApplicationDbContext dbContext, params Ticket[] tickets)
    {
        dbContext.Tickets.AddRange(tickets);
        dbContext.SaveChanges();
    }

    private static void SeedAuditLogs(ApplicationDbContext dbContext, params AuditLog[] auditLogs)
    {
        dbContext.AuditLogs.AddRange(auditLogs);
        dbContext.SaveChanges();
    }

    private static Ticket CreateTicket(
        string title,
        TicketStatus status = TicketStatus.Open,
        TicketPriority priority = TicketPriority.Medium,
        TicketCategory category = TicketCategory.General,
        string? customerEmail = null,
        DateTime? createdAtUtc = null,
        bool isDeleted = false,
        string? description = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = description ?? $"{title} description",
            Status = status,
            Priority = priority,
            Category = category,
            Source = TicketSource.Web,
            CustomerEmail = customerEmail,
            CustomerName = "Test Customer",
            CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow,
            IsDeleted = isDeleted
        };

    private static TicketMessage CreateMessage(
        Guid ticketId,
        string message,
        DateTime createdAtUtc,
        bool isInternalNote = false) =>
        new()
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            Message = message,
            IsInternalNote = isInternalNote,
            CreatedByUserId = "test-user",
            CreatedByDisplayName = "Test User",
            CreatedAtUtc = createdAtUtc
        };

    private static AuditLog CreateAuditLog(string entityName, Guid entityId, string action, DateTime performedAtUtc) =>
        new()
        {
            Id = Guid.NewGuid(),
            EntityName = entityName,
            EntityId = entityId,
            Action = action,
            PerformedAtUtc = performedAtUtc
        };

    private sealed class StubAiTicketAssistantService(
        string suggestedPriority = "High",
        string suggestedCategory = "Billing") : IAiTicketAssistantService
    {
        public Task<TicketAssistantResult> AnalyzeAsync(TicketAssistantRequest request, CancellationToken cancellationToken = default)
        {
            var result = new TicketAssistantResult(
                "AI summary",
                suggestedCategory,
                suggestedPriority,
                "Suggested reply",
                ["billing", "high"],
                "Stub");

            return Task.FromResult(result);
        }

        public TicketReplySuggestionRequest? LastReplySuggestionRequest { get; private set; }

        public TicketSummaryRequest? LastSummaryRequest { get; private set; }

        public TicketTriageRequest? LastTriageRequest { get; private set; }

        public Task<string> SuggestReplyAsync(
            TicketReplySuggestionRequest request,
            CancellationToken cancellationToken = default)
        {
            LastReplySuggestionRequest = request;
            return Task.FromResult("Suggested customer reply.");
        }

        public Task<string> SummarizeTicketAsync(
            TicketSummaryRequest request,
            CancellationToken cancellationToken = default)
        {
            LastSummaryRequest = request;
            return Task.FromResult("Operational ticket summary.");
        }

        public Task<TicketTriageSuggestion> SuggestTriageAsync(
            TicketTriageRequest request,
            CancellationToken cancellationToken = default)
        {
            LastTriageRequest = request;
            return Task.FromResult(new TicketTriageSuggestion(
                TicketPriority.High,
                TicketCategory.Billing,
                true,
                "The customer reports repeated payment failures.",
                "The issue blocks checkout and has repeated failure context."));
        }
    }

    private sealed class StubCurrentUserService(
        bool isAuthenticated = false,
        string? userId = null,
        string? email = null,
        string? fullName = null,
        string? role = null) : ICurrentUserService
    {
        public bool IsAuthenticated { get; } = isAuthenticated;

        public string? UserId { get; } = userId;

        public string? Email { get; } = email;

        public string? FullName { get; } = fullName;

        public string? Role { get; } = role;
    }

    private sealed class StubNotificationService : INotificationService
    {
        public List<CreateNotificationRequest> Requests { get; } = [];

        public Task<NotificationResponse> CreateAsync(
            CreateNotificationRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);

            return Task.FromResult(new NotificationResponse(
                Guid.NewGuid(),
                request.UserId,
                request.Title,
                request.Message,
                request.Type,
                request.TicketId,
                false,
                new DateTimeOffset(DateTime.UtcNow, TimeSpan.Zero),
                null));
        }

        public Task<IReadOnlyList<NotificationResponse>> GetMyNotificationsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<NotificationResponse>>([]);

        public Task<MarkNotificationAsReadResponse?> MarkAsReadAsync(
            Guid notificationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<MarkNotificationAsReadResponse?>(null);

        public Task<MarkAllNotificationsAsReadResponse> MarkAllAsReadAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new MarkAllNotificationsAsReadResponse(0));

        public Task<UnreadNotificationCountResponse> GetUnreadCountAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new UnreadNotificationCountResponse(0));
    }

    private sealed class StubUserLookupService(params string[] invalidAgentIds) : IUserLookupService
    {
        private readonly HashSet<string> invalidAgentIds = new(invalidAgentIds, StringComparer.Ordinal);

        public Task<IReadOnlyList<AgentLookupResponse>> GetAgentsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AgentLookupResponse>>([]);

        public Task<AgentLookupResponse?> GetActiveAgentByIdAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            AgentLookupResponse? response = invalidAgentIds.Contains(userId)
                ? null
                : new AgentLookupResponse(userId, $"{userId}@example.com", "Assigned Agent");

            return Task.FromResult(response);
        }
    }

    private sealed class NoOpHubContext : IHubContext<NotificationHub>
    {
        public IHubClients Clients { get; } = new NoOpHubClients();

        public IGroupManager Groups { get; } = new NoOpGroupManager();
    }

    private sealed class NoOpHubClients : IHubClients
    {
        private static readonly IClientProxy ClientProxy = new NoOpClientProxy();

        public IClientProxy All => ClientProxy;

        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => ClientProxy;

        public IClientProxy Client(string connectionId) => ClientProxy;

        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => ClientProxy;

        public IClientProxy Group(string groupName) => ClientProxy;

        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => ClientProxy;

        public IClientProxy Groups(IReadOnlyList<string> groupNames) => ClientProxy;

        public IClientProxy User(string userId) => ClientProxy;

        public IClientProxy Users(IReadOnlyList<string> userIds) => ClientProxy;
    }

    private sealed class NoOpClientProxy : IClientProxy
    {
        public Task SendCoreAsync(
            string method,
            object?[] args,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class NoOpGroupManager : IGroupManager
    {
        public Task AddToGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveFromGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

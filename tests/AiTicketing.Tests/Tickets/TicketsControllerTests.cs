using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AiTicketing.Application.Ai;
using AiTicketing.Application.Auth;
using AiTicketing.Domain.Entities;
using AiTicketing.Domain.Enums;
using AiTicketing.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace AiTicketing.Tests.Tickets;

public sealed class TicketsControllerTests
{
    private const string Issuer = "AiTicketingAssistant";
    private const string Audience = "AiTicketingAssistant";
    private const string SecretKey = "REPLACE_WITH_A_PRODUCTION_SECRET_KEY_AT_LEAST_32_BYTES";

    [Fact]
    public async Task Swagger_DocumentsTicketPaginationContract()
    {
        await using var factory = CreateFactory(environment: "Development");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/swagger/v1/swagger.json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        var getTickets = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/tickets")
            .GetProperty("get");
        var parameterNames = getTickets
            .GetProperty("parameters")
            .EnumerateArray()
            .Select(parameter => parameter.GetProperty("name").GetString())
            .ToArray();
        var pagedResultProperties = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("TicketDtoPagedResult")
            .GetProperty("properties");
        var getTicketById = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/tickets/{id}")
            .GetProperty("get");
        var getTicketStats = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/tickets/stats")
            .GetProperty("get");
        var getMyTicketStats = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/tickets/my-stats")
            .GetProperty("get");
        var addInternalNote = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/tickets/{id}/internal-notes")
            .GetProperty("post");
        var suggestReply = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/tickets/{id}/ai/suggest-reply")
            .GetProperty("post");
        var summarize = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/tickets/{id}/ai/summarize")
            .GetProperty("post");
        var suggestTriage = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/tickets/{id}/ai/suggest-triage")
            .GetProperty("post");
        var assignTicket = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/tickets/{id}/assign")
            .GetProperty("patch");
        var ticketDetailsProperties = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("TicketDetailsDto")
            .GetProperty("properties");
        var ticketDetailsMessageProperties = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("TicketDetailsMessageDto")
            .GetProperty("properties");
        var ticketStatsProperties = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("TicketStatsResponse")
            .GetProperty("properties");
        var myTicketStatsProperties = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("MyTicketStatsResponse")
            .GetProperty("properties");
        var addInternalNoteRequestProperties = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("AddInternalNoteRequest")
            .GetProperty("properties");
        var addInternalNoteResponseProperties = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("AddInternalNoteResponse")
            .GetProperty("properties");
        var suggestReplyRequestProperties = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("SuggestReplyRequest")
            .GetProperty("properties");
        var suggestedReplyResponseProperties = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("SuggestedReplyResponse")
            .GetProperty("properties");
        var summarizeRequestProperties = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("SummarizeTicketRequest")
            .GetProperty("properties");
        var ticketSummaryResponseProperties = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("TicketSummaryResponse")
            .GetProperty("properties");
        var suggestTriageRequestProperties = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("SuggestTriageRequest")
            .GetProperty("properties");
        var triageSuggestionResponseProperties = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("TicketTriageSuggestionResponse")
            .GetProperty("properties");
        var assignTicketRequestProperties = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("AssignTicketRequest")
            .GetProperty("properties");
        var assignTicketResponseProperties = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("AssignTicketResponse")
            .GetProperty("properties");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(parameterNames, name => string.Equals(name, "Page", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(parameterNames, name => string.Equals(name, "PageSize", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(parameterNames, name => string.Equals(name, "SortBy", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(parameterNames, name => string.Equals(name, "SortDirection", StringComparison.OrdinalIgnoreCase));
        Assert.True(pagedResultProperties.TryGetProperty("page", out _));
        Assert.True(pagedResultProperties.TryGetProperty("pageSize", out _));
        Assert.True(pagedResultProperties.TryGetProperty("totalCount", out _));
        Assert.True(pagedResultProperties.TryGetProperty("totalPages", out _));
        Assert.False(pagedResultProperties.TryGetProperty("pageNumber", out _));
        Assert.True(getTicketById.GetProperty("responses").TryGetProperty("200", out _));
        Assert.True(getTicketById.GetProperty("responses").TryGetProperty("401", out _));
        Assert.True(getTicketById.GetProperty("responses").TryGetProperty("404", out _));
        Assert.False(getTicketById.GetProperty("responses").TryGetProperty("403", out _));
        Assert.True(ticketDetailsProperties.TryGetProperty("messages", out _));
        Assert.True(ticketDetailsMessageProperties.TryGetProperty("senderUserId", out _));
        Assert.True(ticketDetailsMessageProperties.TryGetProperty("senderRole", out _));
        Assert.True(ticketDetailsMessageProperties.TryGetProperty("body", out _));
        Assert.True(getTicketStats.GetProperty("responses").TryGetProperty("200", out _));
        Assert.True(getTicketStats.GetProperty("responses").TryGetProperty("401", out _));
        Assert.True(getTicketStats.GetProperty("responses").TryGetProperty("403", out _));
        Assert.True(ticketStatsProperties.TryGetProperty("total", out _));
        Assert.True(ticketStatsProperties.TryGetProperty("open", out _));
        Assert.True(ticketStatsProperties.TryGetProperty("inProgress", out _));
        Assert.True(ticketStatsProperties.TryGetProperty("resolved", out _));
        Assert.True(ticketStatsProperties.TryGetProperty("closed", out _));
        Assert.True(ticketStatsProperties.TryGetProperty("unassigned", out _));
        Assert.True(ticketStatsProperties.TryGetProperty("lowPriority", out _));
        Assert.True(ticketStatsProperties.TryGetProperty("mediumPriority", out _));
        Assert.True(ticketStatsProperties.TryGetProperty("highPriority", out _));
        Assert.True(ticketStatsProperties.TryGetProperty("urgentPriority", out _));
        Assert.False(myTicketStatsProperties.TryGetProperty("unassigned", out _));
        Assert.True(myTicketStatsProperties.TryGetProperty("total", out _));
        Assert.True(myTicketStatsProperties.TryGetProperty("open", out _));
        Assert.True(myTicketStatsProperties.TryGetProperty("inProgress", out _));
        Assert.True(myTicketStatsProperties.TryGetProperty("resolved", out _));
        Assert.True(myTicketStatsProperties.TryGetProperty("closed", out _));
        Assert.True(myTicketStatsProperties.TryGetProperty("lowPriority", out _));
        Assert.True(myTicketStatsProperties.TryGetProperty("mediumPriority", out _));
        Assert.True(myTicketStatsProperties.TryGetProperty("highPriority", out _));
        Assert.True(myTicketStatsProperties.TryGetProperty("urgentPriority", out _));
        Assert.True(getMyTicketStats.GetProperty("responses").TryGetProperty("200", out _));
        Assert.True(getMyTicketStats.GetProperty("responses").TryGetProperty("401", out _));
        Assert.True(getMyTicketStats.GetProperty("responses").TryGetProperty("403", out _));
        Assert.True(addInternalNote.GetProperty("responses").TryGetProperty("201", out _));
        Assert.True(addInternalNote.GetProperty("responses").TryGetProperty("400", out _));
        Assert.True(addInternalNote.GetProperty("responses").TryGetProperty("401", out _));
        Assert.True(addInternalNote.GetProperty("responses").TryGetProperty("403", out _));
        Assert.True(addInternalNote.GetProperty("responses").TryGetProperty("404", out _));
        Assert.True(addInternalNoteRequestProperties.TryGetProperty("body", out _));
        Assert.True(addInternalNoteResponseProperties.TryGetProperty("message", out _));
        Assert.True(suggestReply.GetProperty("responses").TryGetProperty("200", out _));
        Assert.True(suggestReply.GetProperty("responses").TryGetProperty("400", out _));
        Assert.True(suggestReply.GetProperty("responses").TryGetProperty("401", out _));
        Assert.True(suggestReply.GetProperty("responses").TryGetProperty("403", out _));
        Assert.True(suggestReply.GetProperty("responses").TryGetProperty("404", out _));
        Assert.True(suggestReplyRequestProperties.TryGetProperty("instruction", out _));
        Assert.True(suggestedReplyResponseProperties.TryGetProperty("suggestedReply", out _));
        Assert.True(summarize.GetProperty("responses").TryGetProperty("200", out _));
        Assert.True(summarize.GetProperty("responses").TryGetProperty("400", out _));
        Assert.True(summarize.GetProperty("responses").TryGetProperty("401", out _));
        Assert.True(summarize.GetProperty("responses").TryGetProperty("403", out _));
        Assert.True(summarize.GetProperty("responses").TryGetProperty("404", out _));
        Assert.True(summarize.GetProperty("responses").TryGetProperty("503", out _));
        Assert.True(summarizeRequestProperties.TryGetProperty("includeInternalNotes", out _));
        Assert.True(ticketSummaryResponseProperties.TryGetProperty("summary", out _));
        Assert.True(suggestTriage.GetProperty("responses").TryGetProperty("200", out _));
        Assert.True(suggestTriage.GetProperty("responses").TryGetProperty("400", out _));
        Assert.True(suggestTriage.GetProperty("responses").TryGetProperty("401", out _));
        Assert.True(suggestTriage.GetProperty("responses").TryGetProperty("403", out _));
        Assert.True(suggestTriage.GetProperty("responses").TryGetProperty("404", out _));
        Assert.True(suggestTriage.GetProperty("responses").TryGetProperty("503", out _));
        Assert.True(suggestTriageRequestProperties.TryGetProperty("instruction", out _));
        Assert.True(triageSuggestionResponseProperties.TryGetProperty("currentPriority", out _));
        Assert.True(triageSuggestionResponseProperties.TryGetProperty("suggestedPriority", out _));
        Assert.True(triageSuggestionResponseProperties.TryGetProperty("suggestedCategory", out _));
        Assert.True(triageSuggestionResponseProperties.TryGetProperty("escalationRecommended", out _));
        Assert.True(triageSuggestionResponseProperties.TryGetProperty("escalationReason", out _));
        Assert.True(triageSuggestionResponseProperties.TryGetProperty("rationale", out _));
        Assert.True(assignTicket.GetProperty("responses").TryGetProperty("200", out _));
        Assert.True(assignTicket.GetProperty("responses").TryGetProperty("400", out _));
        Assert.True(assignTicket.GetProperty("responses").TryGetProperty("401", out _));
        Assert.True(assignTicket.GetProperty("responses").TryGetProperty("403", out _));
        Assert.True(assignTicket.GetProperty("responses").TryGetProperty("404", out _));
        Assert.Single(assignTicketRequestProperties.EnumerateObject());
        Assert.True(assignTicketRequestProperties.TryGetProperty("assignedToUserId", out var assignedToUserIdSchema));
        Assert.Equal("string", assignedToUserIdSchema.GetProperty("type").GetString());
        Assert.Equal("uuid", assignedToUserIdSchema.GetProperty("format").GetString());
        Assert.False(assignTicketRequestProperties.TryGetProperty("assignedToDisplayName", out _));
        Assert.False(assignTicketRequestProperties.TryGetProperty("assignedByUserId", out _));
        Assert.False(assignTicketRequestProperties.TryGetProperty("assignedByDisplayName", out _));
        Assert.True(assignTicketResponseProperties.TryGetProperty("ticket", out _));
        Assert.True(assignTicketResponseProperties.TryGetProperty("ticketId", out _));
        Assert.True(assignTicketResponseProperties.TryGetProperty("assignedToUserId", out _));
        Assert.True(assignTicketResponseProperties.TryGetProperty("assignedToDisplayName", out _));
        Assert.True(assignTicketResponseProperties.TryGetProperty("assignedByUserId", out _));
        Assert.True(assignTicketResponseProperties.TryGetProperty("assignedByDisplayName", out _));
        Assert.True(assignTicketResponseProperties.TryGetProperty("assignedAtUtc", out _));
    }

    [Fact]
    public async Task Create_WhenSourceIsStringEnum_ReturnsCreatedTicketWithStringEnums()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var payload = new
        {
            title = "Payment page is not working",
            description = "The customer cannot complete payment and receives an error every time.",
            customerEmail = "customer@example.com",
            customerName = "Sara Ahmed",
            source = "Web"
        };

        using var response = await client.PostAsJsonAsync("/api/tickets", payload);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains("\"source\":\"Web\"", content);
        Assert.Contains("\"status\":\"Open\"", content);
        Assert.Contains("\"priority\":\"High\"", content);
        Assert.Contains("\"category\":\"Billing\"", content);
        Assert.Contains("+00:00", content);
    }

    [Fact]
    public async Task GetById_WhenTicketDoesNotExist_ReturnsNotFound()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        AddAdminToken(client);

        using var response = await client.GetAsync($"/api/tickets/{Guid.NewGuid()}");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Ticket was not found.", content);
    }

    [Fact]
    public async Task GetById_ReturnsStableFrontendDetailsContract()
    {
        var ticketId = Guid.NewGuid();
        var customerMessageId = Guid.NewGuid();
        var agentMessageId = Guid.NewGuid();
        var createdAtUtc = DateTime.UtcNow.AddDays(-2);
        var updatedAtUtc = DateTime.UtcNow.AddDays(-1);
        var resolvedAtUtc = DateTime.UtcNow.AddHours(-2);
        var closedAtUtc = DateTime.UtcNow.AddHours(-1);

        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(new Ticket
            {
                Id = ticketId,
                Title = "Frontend details ticket",
                Description = "Ticket details contract description.",
                Status = TicketStatus.Closed,
                Priority = TicketPriority.High,
                Category = TicketCategory.TechnicalSupport,
                Source = TicketSource.Web,
                CustomerUserId = "customer-user",
                AssignedToUserId = "agent-user",
                CreatedAtUtc = createdAtUtc,
                UpdatedAtUtc = updatedAtUtc,
                ResolvedAtUtc = resolvedAtUtc,
                ClosedAtUtc = closedAtUtc
            });
            dbContext.TicketMessages.AddRange(
                new TicketMessage
                {
                    Id = agentMessageId,
                    TicketId = ticketId,
                    Message = "Agent response",
                    CreatedByUserId = "agent-user",
                    CreatedByDisplayName = "Support Agent",
                    CreatedAtUtc = createdAtUtc.AddHours(2)
                },
                new TicketMessage
                {
                    Id = customerMessageId,
                    TicketId = ticketId,
                    Message = "Customer message",
                    CreatedByUserId = "customer-user",
                    CreatedByDisplayName = "Demo Customer",
                    CreatedAtUtc = createdAtUtc.AddHours(1)
                });
            dbContext.SaveChanges();
        });
        using var client = factory.CreateClient();
        AddAdminToken(client);

        using var response = await client.GetAsync($"/api/tickets/{ticketId}");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = document.RootElement.GetProperty("data");
        var messages = data.GetProperty("messages").EnumerateArray().ToArray();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ticketId, data.GetProperty("id").GetGuid());
        Assert.Equal("Frontend details ticket", data.GetProperty("title").GetString());
        Assert.Equal("Ticket details contract description.", data.GetProperty("description").GetString());
        Assert.Equal("Closed", data.GetProperty("status").GetString());
        Assert.Equal("High", data.GetProperty("priority").GetString());
        Assert.Equal("customer-user", data.GetProperty("customerUserId").GetString());
        Assert.Equal("agent-user", data.GetProperty("assignedToUserId").GetString());
        Assert.Equal(TimeSpan.Zero, data.GetProperty("createdAtUtc").GetDateTimeOffset().Offset);
        Assert.Equal(TimeSpan.Zero, data.GetProperty("updatedAtUtc").GetDateTimeOffset().Offset);
        Assert.Equal(TimeSpan.Zero, data.GetProperty("resolvedAtUtc").GetDateTimeOffset().Offset);
        Assert.Equal(TimeSpan.Zero, data.GetProperty("closedAtUtc").GetDateTimeOffset().Offset);
        Assert.Equal(2, messages.Length);
        Assert.Equal(customerMessageId, messages[0].GetProperty("id").GetGuid());
        Assert.Equal(ticketId, messages[0].GetProperty("ticketId").GetGuid());
        Assert.Equal("customer-user", messages[0].GetProperty("senderUserId").GetString());
        Assert.Equal(AuthRoles.Customer, messages[0].GetProperty("senderRole").GetString());
        Assert.Equal("Customer message", messages[0].GetProperty("body").GetString());
        Assert.Equal(TimeSpan.Zero, messages[0].GetProperty("createdAtUtc").GetDateTimeOffset().Offset);
        Assert.Equal(agentMessageId, messages[1].GetProperty("id").GetGuid());
        Assert.Equal(AuthRoles.Agent, messages[1].GetProperty("senderRole").GetString());
    }

    [Fact]
    public async Task GetStats_ReturnsStableFrontendStatsContract()
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(
                CreateStatsTicket(TicketStatus.Open, TicketPriority.Low),
                CreateStatsTicket(TicketStatus.InProgress, TicketPriority.Medium, assignedToUserId: "agent-1"),
                CreateStatsTicket(TicketStatus.Resolved, TicketPriority.High),
                CreateStatsTicket(TicketStatus.Closed, TicketPriority.Urgent, assignedToUserId: "agent-2"),
                CreateStatsTicket(TicketStatus.Open, TicketPriority.Urgent, isDeleted: true));
            dbContext.SaveChanges();
        });
        using var client = factory.CreateClient();
        AddAdminToken(client);

        using var response = await client.GetAsync("/api/tickets/stats");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = document.RootElement.GetProperty("data");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(4, data.GetProperty("total").GetInt32());
        Assert.Equal(1, data.GetProperty("open").GetInt32());
        Assert.Equal(1, data.GetProperty("inProgress").GetInt32());
        Assert.Equal(1, data.GetProperty("resolved").GetInt32());
        Assert.Equal(1, data.GetProperty("closed").GetInt32());
        Assert.Equal(2, data.GetProperty("unassigned").GetInt32());
        Assert.Equal(1, data.GetProperty("lowPriority").GetInt32());
        Assert.Equal(1, data.GetProperty("mediumPriority").GetInt32());
        Assert.Equal(1, data.GetProperty("highPriority").GetInt32());
        Assert.Equal(1, data.GetProperty("urgentPriority").GetInt32());
    }

    [Fact]
    public async Task GetMyStats_ReturnsStableFrontendStatsContractForCurrentAgent()
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(
                CreateStatsTicket(TicketStatus.Open, TicketPriority.Low, assignedToUserId: "agent-user"),
                CreateStatsTicket(TicketStatus.InProgress, TicketPriority.Medium, assignedToUserId: "agent-user"),
                CreateStatsTicket(TicketStatus.Resolved, TicketPriority.High, assignedToUserId: "other-agent"),
                CreateStatsTicket(
                    TicketStatus.Closed,
                    TicketPriority.Urgent,
                    assignedToUserId: "agent-user",
                    isDeleted: true));
            dbContext.SaveChanges();
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken(AuthRoles.Agent, "agent-user"));

        using var response = await client.GetAsync("/api/tickets/my-stats");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = document.RootElement.GetProperty("data");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, data.GetProperty("total").GetInt32());
        Assert.Equal(1, data.GetProperty("open").GetInt32());
        Assert.Equal(1, data.GetProperty("inProgress").GetInt32());
        Assert.Equal(0, data.GetProperty("resolved").GetInt32());
        Assert.Equal(0, data.GetProperty("closed").GetInt32());
        Assert.Equal(1, data.GetProperty("lowPriority").GetInt32());
        Assert.Equal(1, data.GetProperty("mediumPriority").GetInt32());
        Assert.Equal(0, data.GetProperty("highPriority").GetInt32());
        Assert.Equal(0, data.GetProperty("urgentPriority").GetInt32());
        Assert.False(data.TryGetProperty("unassigned", out _));
    }

    [Fact]
    public async Task AddInternalNote_ReturnsStableFrontendMessageContract()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            var ticket = CreateStatsTicket(
                TicketStatus.Open,
                TicketPriority.Medium,
                assignedToUserId: "agent-user");
            ticket.Id = ticketId;
            ticket.Title = "Internal note contract";
            dbContext.Tickets.Add(ticket);
            dbContext.SaveChanges();
        });
        using var client = factory.CreateClient();
        AddAdminToken(client);

        using var response = await client.PostAsJsonAsync(
            $"/api/tickets/{ticketId}/internal-notes",
            new { body = "  Staff-only note.  " });
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var message = document.RootElement.GetProperty("data").GetProperty("message");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(ticketId, message.GetProperty("ticketId").GetGuid());
        Assert.Equal("Staff-only note.", message.GetProperty("body").GetString());
        Assert.True(message.GetProperty("isInternalNote").GetBoolean());
        Assert.Equal(AuthRoles.Admin, message.GetProperty("senderRole").GetString());
        Assert.True(message.TryGetProperty("id", out _));
        Assert.True(message.TryGetProperty("senderUserId", out _));
        Assert.True(message.TryGetProperty("senderDisplayName", out _));
        Assert.Equal(TimeSpan.Zero, message.GetProperty("createdAtUtc").GetDateTimeOffset().Offset);
    }

    [Fact]
    public async Task SuggestReply_ReturnsStableFrontendContractWithoutCreatingMessage()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            var ticket = CreateStatsTicket(TicketStatus.Open, TicketPriority.High);
            ticket.Id = ticketId;
            ticket.Title = "AI suggestion contract";
            dbContext.Tickets.Add(ticket);
            dbContext.SaveChanges();
        });
        using var client = factory.CreateClient();
        AddAdminToken(client);

        using var response = await client.PostAsJsonAsync(
            $"/api/tickets/{ticketId}/ai/suggest-reply",
            new { instruction = "Keep it friendly." });
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "Suggested reply from controller test AI.",
            document.RootElement.GetProperty("data").GetProperty("suggestedReply").GetString());

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(await dbContext.TicketMessages.ToListAsync());
        Assert.Empty(await dbContext.Notifications.ToListAsync());
        Assert.Empty(await dbContext.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task SuggestReply_WhenInstructionIsOverlong_ReturnsBadRequest()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            var ticket = CreateStatsTicket(TicketStatus.Open, TicketPriority.Medium);
            ticket.Id = ticketId;
            dbContext.Tickets.Add(ticket);
            dbContext.SaveChanges();
        });
        using var client = factory.CreateClient();
        AddAdminToken(client);

        using var response = await client.PostAsJsonAsync(
            $"/api/tickets/{ticketId}/ai/suggest-reply",
            new { instruction = new string('x', 501) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Summarize_ReturnsStableFrontendContractWithoutSideEffects()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            var ticket = CreateStatsTicket(TicketStatus.InProgress, TicketPriority.High);
            ticket.Id = ticketId;
            ticket.Title = "AI summary contract";
            dbContext.Tickets.Add(ticket);
            dbContext.SaveChanges();
        });
        using var client = factory.CreateClient();
        AddAdminToken(client);

        using var response = await client.PostAsJsonAsync(
            $"/api/tickets/{ticketId}/ai/summarize",
            new { includeInternalNotes = false });
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "Ticket summary from controller test AI.",
            document.RootElement.GetProperty("data").GetProperty("summary").GetString());

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(await dbContext.TicketMessages.ToListAsync());
        Assert.Empty(await dbContext.Notifications.ToListAsync());
        Assert.Empty(await dbContext.AuditLogs.ToListAsync());
        var ticket = await dbContext.Tickets.SingleAsync();
        Assert.Equal(TicketStatus.InProgress, ticket.Status);
        Assert.Equal(TicketPriority.High, ticket.Priority);
    }

    [Fact]
    public async Task SuggestTriage_ReturnsStableFrontendContractWithoutSideEffects()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            var ticket = CreateStatsTicket(TicketStatus.InProgress, TicketPriority.Medium);
            ticket.Id = ticketId;
            ticket.Title = "AI triage contract";
            ticket.Category = TicketCategory.Billing;
            dbContext.Tickets.Add(ticket);
            dbContext.SaveChanges();
        });
        using var client = factory.CreateClient();
        AddAdminToken(client);

        using var response = await client.PostAsJsonAsync(
            $"/api/tickets/{ticketId}/ai/suggest-triage",
            new { instruction = "Focus on urgency." });
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = document.RootElement.GetProperty("data");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Medium", data.GetProperty("currentPriority").GetString());
        Assert.Equal("High", data.GetProperty("suggestedPriority").GetString());
        Assert.Equal("Billing", data.GetProperty("suggestedCategory").GetString());
        Assert.True(data.GetProperty("escalationRecommended").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(data.GetProperty("escalationReason").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(data.GetProperty("rationale").GetString()));

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(await dbContext.TicketMessages.ToListAsync());
        Assert.Empty(await dbContext.Notifications.ToListAsync());
        Assert.Empty(await dbContext.AuditLogs.ToListAsync());
        var ticket = await dbContext.Tickets.SingleAsync();
        Assert.Equal(TicketStatus.InProgress, ticket.Status);
        Assert.Equal(TicketPriority.Medium, ticket.Priority);
        Assert.Equal(TicketCategory.Billing, ticket.Category);
        Assert.Null(ticket.AssignedToUserId);
        Assert.Null(ticket.CustomerUserId);
    }

    [Fact]
    public async Task AddMessage_WhenTicketDoesNotExist_ReturnsNotFound()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        AddAdminToken(client);

        var payload = new
        {
            message = "Message for missing ticket.",
            isInternalNote = false,
            createdByUserId = "user-1",
            createdByDisplayName = "Support Agent"
        };

        using var response = await client.PostAsJsonAsync($"/api/tickets/{Guid.NewGuid()}/messages", payload);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Ticket was not found.", content);
    }

    [Fact]
    public async Task ChangeStatus_WhenTicketDoesNotExist_ReturnsNotFound()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        AddAdminToken(client);

        var payload = new
        {
            status = "InProgress",
            changedByUserId = "user-1",
            changedByDisplayName = "Support Agent"
        };

        using var response = await client.PatchAsJsonAsync($"/api/tickets/{Guid.NewGuid()}/status", payload);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Ticket was not found.", content);
    }

    [Fact]
    public async Task Assign_WhenTicketDoesNotExist_ReturnsNotFound()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        AddAdminToken(client);

        var payload = new
        {
            assignedToUserId = "11111111-1111-1111-1111-111111111111"
        };

        using var response = await client.PatchAsJsonAsync($"/api/tickets/{Guid.NewGuid()}/assign", payload);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Ticket was not found.", content);
    }

    [Fact]
    public async Task GetAuditLogs_WhenTicketDoesNotExist_ReturnsNotFound()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        AddAdminToken(client);

        using var response = await client.GetAsync($"/api/tickets/{Guid.NewGuid()}/audit-logs");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Ticket was not found.", content);
    }

    [Fact]
    public async Task Delete_WhenTicketDoesNotExist_ReturnsNotFound()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        AddAdminToken(client);

        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/tickets/{Guid.NewGuid()}")
        {
            Content = JsonContent.Create(new
            {
                deletedByUserId = "user-1",
                deletedByDisplayName = "Support Manager",
                reason = "Duplicate ticket"
            })
        };

        using var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Ticket was not found.", content);
    }

    [Fact]
    public async Task ChangeStatus_WhenResolved_ReturnsResolvedAtUtcWithUtcOffset()
    {
        var ticketId = Guid.NewGuid();

        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(new Ticket
            {
                Id = ticketId,
                Title = "Ticket to resolve",
                Description = "Ticket to resolve description",
                Status = TicketStatus.Open,
                Priority = TicketPriority.Medium,
                Category = TicketCategory.General,
                Source = TicketSource.Web,
                CreatedAtUtc = DateTime.UtcNow
            });
            dbContext.SaveChanges();
        });
        using var client = factory.CreateClient();
        AddAdminToken(client);

        var payload = new
        {
            status = "Resolved",
            changedByUserId = "user-1",
            changedByDisplayName = "Support Agent"
        };

        using var response = await client.PatchAsJsonAsync($"/api/tickets/{ticketId}/status", payload);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"resolvedAtUtc\":\"", content);
        Assert.Contains("+00:00", content);
    }

    [Fact]
    public async Task ChangeStatus_WhenClosed_ReturnsClosedAtUtcWithUtcOffset()
    {
        var ticketId = Guid.NewGuid();

        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(new Ticket
            {
                Id = ticketId,
                Title = "Ticket to close",
                Description = "Ticket to close description",
                Status = TicketStatus.Open,
                Priority = TicketPriority.Medium,
                Category = TicketCategory.General,
                Source = TicketSource.Web,
                CreatedAtUtc = DateTime.UtcNow
            });
            dbContext.SaveChanges();
        });
        using var client = factory.CreateClient();
        AddAdminToken(client);

        var payload = new
        {
            status = "Closed",
            changedByUserId = "user-1",
            changedByDisplayName = "Support Agent"
        };

        using var response = await client.PatchAsJsonAsync($"/api/tickets/{ticketId}/status", payload);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"closedAtUtc\":\"", content);
        Assert.Contains("+00:00", content);
    }

    private static WebApplicationFactory<Program> CreateFactory(
        Action<ApplicationDbContext>? seed = null,
        string? environment = null)
    {
        var databaseName = Guid.NewGuid().ToString();

        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                if (environment is not null)
                {
                    builder.UseEnvironment(environment);
                }

                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                    services.RemoveAll<IAiTicketAssistantService>();

                    services.AddDbContext<ApplicationDbContext>(options =>
                        options.UseInMemoryDatabase(databaseName));

                    services.AddSingleton<IAiTicketAssistantService, TestAiTicketAssistantService>();

                    if (seed is not null)
                    {
                        var serviceProvider = services.BuildServiceProvider();
                        using var scope = serviceProvider.CreateScope();
                        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        seed(dbContext);
                    }
                });
            });
    }

    private static void AddAdminToken(HttpClient client) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(AuthRoles.Admin));

    private static Ticket CreateStatsTicket(
        TicketStatus status,
        TicketPriority priority,
        string? assignedToUserId = null,
        bool isDeleted = false) =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = $"{status} {priority} ticket",
            Description = "Ticket stats contract test.",
            Status = status,
            Priority = priority,
            Category = TicketCategory.General,
            Source = TicketSource.Web,
            AssignedToUserId = assignedToUserId,
            CreatedAtUtc = DateTime.UtcNow,
            IsDeleted = isDeleted
        };

    private static string CreateToken(string role, string? userId = null)
    {
        var now = DateTime.UtcNow;
        userId ??= Guid.NewGuid().ToString();
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(JwtRegisteredClaimNames.Email, $"{role.ToLowerInvariant()}@example.com"),
            new Claim(ClaimTypes.Email, $"{role.ToLowerInvariant()}@example.com"),
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, $"{role} User"),
            new Claim(ClaimTypes.Role, role),
            new Claim("role", role)
        };

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: now.AddMinutes(-1),
            expires: now.AddMinutes(30),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class TestAiTicketAssistantService : IAiTicketAssistantService
    {
        public Task<TicketAssistantResult> AnalyzeAsync(TicketAssistantRequest request, CancellationToken cancellationToken = default)
        {
            var result = new TicketAssistantResult(
                "Payment is failing at checkout.",
                "Billing",
                "High",
                "Thanks for reporting this. We are checking the payment error now.",
                ["billing", "payment"],
                "Test");

            return Task.FromResult(result);
        }

        public Task<string> SuggestReplyAsync(
            TicketReplySuggestionRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult("Suggested reply from controller test AI.");

        public Task<string> SummarizeTicketAsync(
            TicketSummaryRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult("Ticket summary from controller test AI.");

        public Task<TicketTriageSuggestion> SuggestTriageAsync(
            TicketTriageRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new TicketTriageSuggestion(
                TicketPriority.High,
                TicketCategory.Billing,
                true,
                "The customer reports repeated payment failures.",
                "The issue blocks checkout and may need faster support review."));
    }
}

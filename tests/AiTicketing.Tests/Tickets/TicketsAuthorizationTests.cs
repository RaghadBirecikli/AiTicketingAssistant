using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiTicketing.Application.Ai;
using AiTicketing.Application.Auth;
using AiTicketing.Application.Common.Models;
using AiTicketing.Application.Tickets;
using AiTicketing.Domain.Entities;
using AiTicketing.Domain.Enums;
using AiTicketing.Infrastructure.Ai;
using AiTicketing.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace AiTicketing.Tests.Tickets;

public sealed class TicketsAuthorizationTests
{
    private const string Issuer = "AiTicketingAssistant";
    private const string Audience = "AiTicketingAssistant";
    private const string SecretKey = "REPLACE_WITH_A_PRODUCTION_SECRET_KEY_AT_LEAST_32_BYTES";
    private const string AssignmentAgentUserId = "11111111-1111-1111-1111-111111111111";
    private const string AssignmentCustomerUserId = "22222222-2222-2222-2222-222222222222";
    private const string UnknownAssignmentAgentUserId = "33333333-3333-3333-3333-333333333333";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData(AuthRoles.Customer, HttpStatusCode.Forbidden)]
    [InlineData(AuthRoles.Agent, HttpStatusCode.Forbidden)]
    [InlineData(AuthRoles.Admin, HttpStatusCode.OK)]
    public async Task Assign_AuthorizesAdminOnly(string? role, HttpStatusCode expectedStatusCode)
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            SeedAgent(dbContext, AssignmentAgentUserId);
            dbContext.Tickets.Add(CreateTicket(ticketId));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, role);

        using var response = await client.PatchAsJsonAsync($"/api/tickets/{ticketId}/assign", new
        {
            assignedToUserId = AssignmentAgentUserId
        });

        Assert.Equal(expectedStatusCode, response.StatusCode);
    }

    [Fact]
    public async Task Assign_WhenAdminAssignsTicket_CreatesNotificationForAssignedAgent()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            SeedAgent(dbContext, AssignmentAgentUserId);
            dbContext.Tickets.Add(CreateTicket(ticketId, title: "Notification assignment ticket"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var assignResponse = await client.PatchAsJsonAsync($"/api/tickets/{ticketId}/assign", new
        {
            assignedToUserId = AssignmentAgentUserId
        });

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken(AuthRoles.Agent, AssignmentAgentUserId));
        using var notificationsResponse = await client.GetAsync("/api/notifications");
        var content = await notificationsResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, assignResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, notificationsResponse.StatusCode);
        Assert.Contains("\"type\":\"TicketAssigned\"", content);
        Assert.Contains("\"title\":\"Ticket assigned\"", content);
        Assert.Contains(ticketId.ToString(), content);
    }

    [Fact]
    public async Task Assign_WhenAssignedUserDoesNotExist_ReturnsBadRequestWithoutPartialPersistence()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(ticketId));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.PatchAsJsonAsync($"/api/tickets/{ticketId}/assign", new
        {
            assignedToUserId = UnknownAssignmentAgentUserId
        });

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ticket = await dbContext.Tickets.SingleAsync(ticket => ticket.Id == ticketId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(ticket.AssignedToUserId);
        Assert.Empty(await dbContext.AuditLogs.ToListAsync());
        Assert.Empty(await dbContext.Notifications.ToListAsync());
    }

    [Fact]
    public async Task Assign_WhenAssignedUserIsNotAgent_ReturnsBadRequestWithoutUnhandledException()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            SeedCustomer(dbContext, AssignmentCustomerUserId);
            dbContext.Tickets.Add(CreateTicket(ticketId));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.PatchAsJsonAsync($"/api/tickets/{ticketId}/assign", new
        {
            assignedToUserId = AssignmentCustomerUserId
        });
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("AssignedToUserId must reference an active Agent user.", content);
        Assert.DoesNotContain("An unexpected error occurred.", content);
    }

    [Fact]
    public async Task Assign_WhenAdminSendsMinimalBody_PersistsAssignmentAuditAndNotificationOnce()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            SeedAgent(dbContext, AssignmentAgentUserId);
            dbContext.Tickets.Add(CreateTicket(ticketId, title: "Minimal assignment ticket"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.PatchAsJsonAsync($"/api/tickets/{ticketId}/assign", new
        {
            assignedToUserId = AssignmentAgentUserId
        });
        var body = await response.Content.ReadAsStringAsync();

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ticket = await dbContext.Tickets.SingleAsync(ticket => ticket.Id == ticketId);
        var auditLogs = await dbContext.AuditLogs.ToListAsync();
        var notifications = await dbContext.Notifications.ToListAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"assignedToUserId\":\"11111111-1111-1111-1111-111111111111\"", body);
        Assert.Contains("\"assignedToDisplayName\":\"11111111-1111-1111-1111-111111111111\"", body);
        Assert.Contains("\"assignedByUserId\":\"admin-user\"", body);
        Assert.Contains("\"assignedByDisplayName\":\"Admin User\"", body);
        Assert.Contains("\"assignedAtUtc\":\"", body);
        Assert.Equal(AssignmentAgentUserId, ticket.AssignedToUserId);
        Assert.Single(auditLogs);
        Assert.Equal("TicketAssigned", auditLogs[0].Action);
        Assert.Equal("admin-user", auditLogs[0].PerformedByUserId);
        Assert.Equal("Admin User", auditLogs[0].PerformedByDisplayName);
        Assert.Single(notifications);
        Assert.Equal(AssignmentAgentUserId, notifications[0].UserId);
        Assert.Equal("TicketAssigned", notifications[0].Type);
    }

    [Fact]
    public async Task Assign_WhenClientSendsExtraActorFields_IgnoresThemAndUsesAuthenticatedUser()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            SeedAgent(dbContext, AssignmentAgentUserId);
            dbContext.Tickets.Add(CreateTicket(ticketId));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.PatchAsJsonAsync($"/api/tickets/{ticketId}/assign", new
        {
            assignedToUserId = AssignmentAgentUserId,
            assignedToDisplayName = "Spoofed Agent",
            assignedByUserId = "spoofed-admin",
            assignedByDisplayName = "Spoofed Admin"
        });
        var body = await response.Content.ReadAsStringAsync();

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var auditLog = await dbContext.AuditLogs.SingleAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("Spoofed Agent", body);
        Assert.DoesNotContain("spoofed-admin", body);
        Assert.DoesNotContain("Spoofed Admin", body);
        Assert.Equal("admin-user", auditLog.PerformedByUserId);
        Assert.Equal("Admin User", auditLog.PerformedByDisplayName);
    }

    [Fact]
    public async Task Assign_WhenAssignedToUserIdIsEmptyGuid_ReturnsBadRequestWithoutPartialPersistence()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(ticketId));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.PatchAsJsonAsync($"/api/tickets/{ticketId}/assign", new
        {
            assignedToUserId = Guid.Empty
        });

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ticket = await dbContext.Tickets.SingleAsync(ticket => ticket.Id == ticketId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(ticket.AssignedToUserId);
        Assert.Empty(await dbContext.AuditLogs.ToListAsync());
        Assert.Empty(await dbContext.Notifications.ToListAsync());
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData(AuthRoles.Customer, HttpStatusCode.Forbidden)]
    [InlineData(AuthRoles.Agent, HttpStatusCode.Forbidden)]
    [InlineData(AuthRoles.Admin, HttpStatusCode.OK)]
    public async Task Delete_AuthorizesAdminOnly(string? role, HttpStatusCode expectedStatusCode)
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(ticketId));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, role);
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/tickets/{ticketId}")
        {
            Content = JsonContent.Create(new
            {
                deletedByUserId = "request-admin",
                deletedByDisplayName = "Request Admin",
                reason = "Duplicate ticket"
            })
        };

        using var response = await client.SendAsync(request);

        Assert.Equal(expectedStatusCode, response.StatusCode);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData(AuthRoles.Customer, HttpStatusCode.Forbidden)]
    [InlineData(AuthRoles.Agent, HttpStatusCode.Forbidden)]
    [InlineData(AuthRoles.Admin, HttpStatusCode.OK)]
    public async Task GetAuditLogs_AuthorizesAdminOnly(string? role, HttpStatusCode expectedStatusCode)
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(ticketId));
            dbContext.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                EntityName = nameof(Ticket),
                EntityId = ticketId,
                Action = "TicketCreated",
                PerformedAtUtc = DateTime.UtcNow
            });
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, role);

        using var response = await client.GetAsync($"/api/tickets/{ticketId}/audit-logs");

        Assert.Equal(expectedStatusCode, response.StatusCode);
    }

    [Fact]
    public async Task GetTickets_WithoutToken_ReturnsUnauthorized()
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(Guid.NewGuid()));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, role: null);

        using var response = await client.GetAsync("/api/tickets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData(AuthRoles.Customer, HttpStatusCode.Forbidden)]
    [InlineData(AuthRoles.Agent, HttpStatusCode.Forbidden)]
    [InlineData(AuthRoles.Admin, HttpStatusCode.OK)]
    public async Task GetTicketStats_AuthorizesAdminOnly(string? role, HttpStatusCode expectedStatusCode)
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(Guid.NewGuid()));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, role, userId: "test-user");

        using var response = await client.GetAsync("/api/tickets/stats");

        Assert.Equal(expectedStatusCode, response.StatusCode);
    }

    [Theory]
    [InlineData(null, null, HttpStatusCode.Unauthorized)]
    [InlineData(AuthRoles.Customer, "customer-user", HttpStatusCode.Forbidden)]
    [InlineData(AuthRoles.Admin, "admin-user", HttpStatusCode.Created)]
    [InlineData(AuthRoles.Agent, "agent-user", HttpStatusCode.Created)]
    public async Task AddInternalNote_AuthorizesAdminAndAssignedAgentOnly(
        string? role,
        string? userId,
        HttpStatusCode expectedStatusCode)
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(ticketId, assignedToUserId: "agent-user", customerUserId: "customer-user"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, role, userId);

        using var response = await PostInternalNoteAsync(client, ticketId, "Internal investigation note.");

        Assert.Equal(expectedStatusCode, response.StatusCode);
    }

    [Fact]
    public async Task AddInternalNote_WhenAgentIsNotAssigned_ReturnsNotFound()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(ticketId, assignedToUserId: "other-agent"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Agent, "agent-user");

        using var response = await PostInternalNoteAsync(client, ticketId, "Inaccessible note.");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AddInternalNote_WhenTicketIsMissingOrDeleted_ReturnsNotFound(bool deleted)
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            if (deleted)
            {
                dbContext.Tickets.Add(CreateTicket(ticketId, isDeleted: true));
                dbContext.SaveChanges();
            }
        });
        using var client = CreateClient(factory, AuthRoles.Admin, "admin-user");

        using var response = await PostInternalNoteAsync(client, ticketId, "Unavailable ticket note.");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AddInternalNote_WhenBodyIsEmpty_ReturnsBadRequest(string body)
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(ticketId));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, "admin-user");

        using var response = await PostInternalNoteAsync(client, ticketId, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(null, null, HttpStatusCode.Unauthorized)]
    [InlineData(AuthRoles.Customer, "customer-user", HttpStatusCode.Forbidden)]
    [InlineData(AuthRoles.Admin, "admin-user", HttpStatusCode.OK)]
    [InlineData(AuthRoles.Agent, "agent-user", HttpStatusCode.OK)]
    public async Task SuggestReply_AuthorizesAdminAndAssignedAgentOnly(
        string? role,
        string? userId,
        HttpStatusCode expectedStatusCode)
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(ticketId, assignedToUserId: "agent-user", customerUserId: "customer-user"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, role, userId);

        using var response = await client.PostAsync($"/api/tickets/{ticketId}/ai/suggest-reply", content: null);

        Assert.Equal(expectedStatusCode, response.StatusCode);
    }

    [Fact]
    public async Task SuggestReply_WhenAgentIsNotAssigned_ReturnsNotFound()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(ticketId, assignedToUserId: "other-agent"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Agent, "agent-user");

        using var response = await client.PostAsync($"/api/tickets/{ticketId}/ai/suggest-reply", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuggestReply_WhenTicketIsMissingOrDeleted_ReturnsNotFound(bool deleted)
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            if (deleted)
            {
                dbContext.Tickets.Add(CreateTicket(ticketId, isDeleted: true));
                dbContext.SaveChanges();
            }
        });
        using var client = CreateClient(factory, AuthRoles.Admin, "admin-user");

        using var response = await client.PostAsync($"/api/tickets/{ticketId}/ai/suggest-reply", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(null, null, HttpStatusCode.Unauthorized)]
    [InlineData(AuthRoles.Customer, "customer-user", HttpStatusCode.Forbidden)]
    [InlineData(AuthRoles.Admin, "admin-user", HttpStatusCode.OK)]
    [InlineData(AuthRoles.Agent, "agent-user", HttpStatusCode.OK)]
    public async Task Summarize_AuthorizesAdminAndAssignedAgentOnly(
        string? role,
        string? userId,
        HttpStatusCode expectedStatusCode)
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(ticketId, assignedToUserId: "agent-user", customerUserId: "customer-user"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, role, userId);

        using var response = await client.PostAsync($"/api/tickets/{ticketId}/ai/summarize", content: null);

        Assert.Equal(expectedStatusCode, response.StatusCode);
    }

    [Fact]
    public async Task Summarize_WhenAgentRequestsInternalNotes_ReturnsForbidden()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(ticketId, assignedToUserId: "agent-user"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Agent, "agent-user");

        using var response = await client.PostAsJsonAsync(
            $"/api/tickets/{ticketId}/ai/summarize",
            new { includeInternalNotes = true });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Summarize_WhenAgentIsNotAssigned_ReturnsNotFound()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(ticketId, assignedToUserId: "other-agent"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Agent, "agent-user");

        using var response = await client.PostAsync($"/api/tickets/{ticketId}/ai/summarize", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Summarize_WhenTicketIsMissingOrDeleted_ReturnsNotFound(bool deleted)
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            if (deleted)
            {
                dbContext.Tickets.Add(CreateTicket(ticketId, isDeleted: true));
                dbContext.SaveChanges();
            }
        });
        using var client = CreateClient(factory, AuthRoles.Admin, "admin-user");

        using var response = await client.PostAsync($"/api/tickets/{ticketId}/ai/summarize", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(null, null, HttpStatusCode.Unauthorized)]
    [InlineData(AuthRoles.Customer, "customer-user", HttpStatusCode.Forbidden)]
    [InlineData(AuthRoles.Admin, "admin-user", HttpStatusCode.OK)]
    [InlineData(AuthRoles.Agent, "agent-user", HttpStatusCode.OK)]
    public async Task SuggestTriage_AuthorizesAdminAndAssignedAgentOnly(
        string? role,
        string? userId,
        HttpStatusCode expectedStatusCode)
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(ticketId, assignedToUserId: "agent-user", customerUserId: "customer-user"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, role, userId);

        using var response = await client.PostAsync($"/api/tickets/{ticketId}/ai/suggest-triage", content: null);

        Assert.Equal(expectedStatusCode, response.StatusCode);
    }

    [Fact]
    public async Task SuggestTriage_WhenRequestBodyIsEmpty_ReturnsOk()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(ticketId));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, "admin-user");

        using var response = await client.PostAsync($"/api/tickets/{ticketId}/ai/suggest-triage", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SuggestTriage_WhenInstructionIsOverlong_ReturnsBadRequest()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(ticketId));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, "admin-user");

        using var response = await client.PostAsJsonAsync(
            $"/api/tickets/{ticketId}/ai/suggest-triage",
            new { instruction = new string('x', 501) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SuggestTriage_WhenAgentIsNotAssigned_ReturnsNotFound()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(ticketId, assignedToUserId: "other-agent"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Agent, "agent-user");

        using var response = await client.PostAsync($"/api/tickets/{ticketId}/ai/suggest-triage", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuggestTriage_WhenTicketIsMissingOrDeleted_ReturnsNotFound(bool deleted)
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            if (deleted)
            {
                dbContext.Tickets.Add(CreateTicket(ticketId, isDeleted: true));
                dbContext.SaveChanges();
            }
        });
        using var client = CreateClient(factory, AuthRoles.Admin, "admin-user");

        using var response = await client.PostAsync($"/api/tickets/{ticketId}/ai/suggest-triage", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AiEndpoints_ShareRateLimitQuotaPerAuthenticatedUser()
    {
        var ticketId = Guid.NewGuid();
        var telemetry = new TestAiOperationTelemetry();
        await using var factory = CreateFactory(
            dbContext =>
            {
                dbContext.Tickets.Add(CreateTicket(ticketId, assignedToUserId: "agent-user"));
                dbContext.SaveChanges();
            },
            CreateRateLimitConfiguration(permitLimit: 2),
            telemetry);
        using var client = CreateClient(factory, AuthRoles.Admin, "admin-user");

        using var first = await client.PostAsync($"/api/tickets/{ticketId}/ai/suggest-reply", content: null);
        using var second = await client.PostAsync($"/api/tickets/{ticketId}/ai/summarize", content: null);
        using var third = await client.PostAsync($"/api/tickets/{ticketId}/ai/suggest-triage", content: null);
        var thirdContent = await third.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.True(third.Headers.RetryAfter is not null || third.Headers.Contains("Retry-After"));
        Assert.Contains("\"success\":false", thirdContent);
        Assert.Contains("Too many AI requests. Please try again later.", thirdContent);
        Assert.Equal(
            [AiOperationNames.SuggestReply, AiOperationNames.Summarize, AiOperationNames.SuggestTriage],
            telemetry.Records.Select(record => record.OperationName));
        Assert.Equal(AiOperationOutcomes.RateLimited, telemetry.Records.Last().Outcome);
        Assert.All(telemetry.Records, record =>
        {
            Assert.True(record.DurationMilliseconds >= 0);
            Assert.Equal(AiAssistantProviders.RuleBased, record.ProviderCategory);
        });
    }

    [Fact]
    public async Task AiRateLimit_UsesSeparateQuotaForDifferentAuthenticatedUsers()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(
            dbContext =>
            {
                dbContext.Tickets.Add(CreateTicket(ticketId, assignedToUserId: "agent-user"));
                dbContext.SaveChanges();
            },
            CreateRateLimitConfiguration(permitLimit: 1));
        using var adminClient = CreateClient(factory, AuthRoles.Admin, "admin-user");
        using var agentClient = CreateClient(factory, AuthRoles.Agent, "agent-user");

        using var adminFirst = await adminClient.PostAsync($"/api/tickets/{ticketId}/ai/suggest-reply", content: null);
        using var adminSecond = await adminClient.PostAsync($"/api/tickets/{ticketId}/ai/summarize", content: null);
        using var agentFirst = await agentClient.PostAsync($"/api/tickets/{ticketId}/ai/suggest-triage", content: null);

        Assert.Equal(HttpStatusCode.OK, adminFirst.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, adminSecond.StatusCode);
        Assert.Equal(HttpStatusCode.OK, agentFirst.StatusCode);
    }

    [Fact]
    public async Task AiRateLimit_AnonymousRequestsStillReturnUnauthorized()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(
            dbContext =>
            {
                dbContext.Tickets.Add(CreateTicket(ticketId));
                dbContext.SaveChanges();
            },
            CreateRateLimitConfiguration(permitLimit: 1));
        using var adminClient = CreateClient(factory, AuthRoles.Admin, "admin-user");
        using var anonymousClient = factory.CreateClient();

        using var adminFirst = await adminClient.PostAsync($"/api/tickets/{ticketId}/ai/suggest-reply", content: null);
        using var adminSecond = await adminClient.PostAsync($"/api/tickets/{ticketId}/ai/summarize", content: null);
        using var anonymous = await anonymousClient.PostAsync($"/api/tickets/{ticketId}/ai/suggest-triage", content: null);

        Assert.Equal(HttpStatusCode.OK, adminFirst.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, adminSecond.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task AiTelemetryFailure_DoesNotFailSuccessfulAiRequest()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(
            dbContext =>
            {
                dbContext.Tickets.Add(CreateTicket(ticketId));
                dbContext.SaveChanges();
            },
            CreateRateLimitConfiguration(permitLimit: 10),
            new ThrowingAiOperationTelemetry());
        using var client = CreateClient(factory, AuthRoles.Admin, "admin-user");

        using var response = await client.PostAsync($"/api/tickets/{ticketId}/ai/suggest-reply", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData(AuthRoles.Admin, HttpStatusCode.Forbidden)]
    [InlineData(AuthRoles.Agent, HttpStatusCode.OK)]
    [InlineData(AuthRoles.Customer, HttpStatusCode.OK)]
    public async Task GetMyTicketStats_AuthorizesAgentAndCustomerOnly(string? role, HttpStatusCode expectedStatusCode)
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(
                Guid.NewGuid(),
                assignedToUserId: "test-user",
                customerUserId: "test-user"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, role, userId: "test-user");

        using var response = await client.GetAsync("/api/tickets/my-stats");

        Assert.Equal(expectedStatusCode, response.StatusCode);
    }

    [Fact]
    public async Task GetTickets_WhenAdmin_ReturnsAllNonDeletedTickets()
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(
                CreateTicket(Guid.NewGuid(), title: "Visible one"),
                CreateTicket(Guid.NewGuid(), title: "Visible two"),
                CreateTicket(Guid.NewGuid(), title: "Deleted one", isDeleted: true));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.GetAsync("/api/tickets");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Equal(2, body.Data.TotalCount);
        Assert.Contains(body.Data.Items, ticket => ticket.Title == "Visible one");
        Assert.Contains(body.Data.Items, ticket => ticket.Title == "Visible two");
        Assert.DoesNotContain(body.Data.Items, ticket => ticket.Title == "Deleted one");
    }

    [Fact]
    public async Task GetTickets_WhenAgent_ReturnsOnlyAssignedTickets()
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(
                CreateTicket(Guid.NewGuid(), title: "Assigned to agent", assignedToUserId: "agent-user"),
                CreateTicket(Guid.NewGuid(), title: "Assigned to someone else", assignedToUserId: "other-agent"),
                CreateTicket(Guid.NewGuid(), title: "Unassigned ticket"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Agent, userId: "agent-user");

        using var response = await client.GetAsync("/api/tickets");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Single(body.Data.Items);
        Assert.Equal("Assigned to agent", body.Data.Items[0].Title);
    }

    [Fact]
    public async Task GetTickets_WhenCustomer_ReturnsOnlyOwnedTickets()
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(
                CreateTicket(Guid.NewGuid(), title: "Owned by customer", customerUserId: "customer-user"),
                CreateTicket(Guid.NewGuid(), title: "Owned by another customer", customerUserId: "other-customer"),
                CreateTicket(Guid.NewGuid(), title: "Anonymous ticket"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Customer, userId: "customer-user");

        using var response = await client.GetAsync("/api/tickets");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Single(body.Data.Items);
        Assert.Equal("Owned by customer", body.Data.Items[0].Title);
    }

    [Fact]
    public async Task GetTickets_WhenCustomer_DoesNotReturnDeletedOwnedTickets()
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(
                CreateTicket(Guid.NewGuid(), title: "Visible owned ticket", customerUserId: "customer-user"),
                CreateTicket(Guid.NewGuid(), title: "Deleted owned ticket", customerUserId: "customer-user", isDeleted: true));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Customer, userId: "customer-user");

        using var response = await client.GetAsync("/api/tickets");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Single(body.Data.Items);
        Assert.Equal("Visible owned ticket", body.Data.Items[0].Title);
    }

    [Theory]
    [InlineData(TicketStatus.Open, "Open ticket")]
    [InlineData(TicketStatus.InProgress, "In progress ticket")]
    public async Task GetTickets_WhenAdminFiltersByStatus_ReturnsOnlyMatchingTickets(
        TicketStatus status,
        string expectedTitle)
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(
                CreateTicket(Guid.NewGuid(), title: "Open ticket", status: TicketStatus.Open),
                CreateTicket(Guid.NewGuid(), title: "In progress ticket", status: TicketStatus.InProgress),
                CreateTicket(Guid.NewGuid(), title: "Resolved ticket", status: TicketStatus.Resolved));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.GetAsync($"/api/tickets?status={status}");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Single(body.Data.Items);
        Assert.Equal(expectedTitle, body.Data.Items[0].Title);
        Assert.Equal(status, body.Data.Items[0].Status);
    }

    [Fact]
    public async Task GetTickets_WhenAgentFiltersByStatus_ReturnsOnlyAssignedMatchingTickets()
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(
                CreateTicket(Guid.NewGuid(), title: "Assigned matching", assignedToUserId: "agent-user", status: TicketStatus.Resolved),
                CreateTicket(Guid.NewGuid(), title: "Assigned other status", assignedToUserId: "agent-user", status: TicketStatus.Open),
                CreateTicket(Guid.NewGuid(), title: "Other agent matching", assignedToUserId: "other-agent", status: TicketStatus.Resolved));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Agent, userId: "agent-user");

        using var response = await client.GetAsync("/api/tickets?status=Resolved");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Single(body.Data.Items);
        Assert.Equal("Assigned matching", body.Data.Items[0].Title);
    }

    [Fact]
    public async Task GetTickets_WhenCustomerFiltersByStatus_ReturnsOnlyOwnedMatchingTickets()
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(
                CreateTicket(Guid.NewGuid(), title: "Owned matching", customerUserId: "customer-user", status: TicketStatus.Closed),
                CreateTicket(Guid.NewGuid(), title: "Owned other status", customerUserId: "customer-user", status: TicketStatus.Open),
                CreateTicket(Guid.NewGuid(), title: "Other customer matching", customerUserId: "other-customer", status: TicketStatus.Closed));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Customer, userId: "customer-user");

        using var response = await client.GetAsync("/api/tickets?status=Closed");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Single(body.Data.Items);
        Assert.Equal("Owned matching", body.Data.Items[0].Title);
    }

    [Fact]
    public async Task GetTickets_WhenFilteringByStatus_ExcludesDeletedTickets()
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(
                CreateTicket(Guid.NewGuid(), title: "Visible resolved", status: TicketStatus.Resolved),
                CreateTicket(Guid.NewGuid(), title: "Deleted resolved", status: TicketStatus.Resolved, isDeleted: true));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.GetAsync("/api/tickets?status=Resolved");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Single(body.Data.Items);
        Assert.Equal("Visible resolved", body.Data.Items[0].Title);
    }

    [Fact]
    public async Task GetTickets_WhenStatusIsInvalid_ReturnsBadRequest()
    {
        await using var factory = CreateFactory(_ => { });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.GetAsync("/api/tickets?status=NotAStatus");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(TicketPriority.Low, "Low priority ticket")]
    [InlineData(TicketPriority.High, "High priority ticket")]
    [InlineData(TicketPriority.Urgent, "Urgent priority ticket")]
    public async Task GetTickets_WhenAdminFiltersByPriority_ReturnsOnlyMatchingTickets(
        TicketPriority priority,
        string expectedTitle)
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(
                CreateTicket(Guid.NewGuid(), title: "Low priority ticket", priority: TicketPriority.Low),
                CreateTicket(Guid.NewGuid(), title: "High priority ticket", priority: TicketPriority.High),
                CreateTicket(Guid.NewGuid(), title: "Urgent priority ticket", priority: TicketPriority.Urgent));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.GetAsync($"/api/tickets?priority={priority}");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Single(body.Data.Items);
        Assert.Equal(expectedTitle, body.Data.Items[0].Title);
        Assert.Equal(priority, body.Data.Items[0].Priority);
    }

    [Fact]
    public async Task GetTickets_WhenAgentFiltersByPriority_ReturnsOnlyAssignedMatchingTickets()
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(
                CreateTicket(Guid.NewGuid(), title: "Assigned urgent", assignedToUserId: "agent-user", priority: TicketPriority.Urgent),
                CreateTicket(Guid.NewGuid(), title: "Assigned low", assignedToUserId: "agent-user", priority: TicketPriority.Low),
                CreateTicket(Guid.NewGuid(), title: "Other agent urgent", assignedToUserId: "other-agent", priority: TicketPriority.Urgent));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Agent, userId: "agent-user");

        using var response = await client.GetAsync("/api/tickets?priority=Urgent");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Single(body.Data.Items);
        Assert.Equal("Assigned urgent", body.Data.Items[0].Title);
    }

    [Fact]
    public async Task GetTickets_WhenCustomerFiltersByPriority_ReturnsOnlyOwnedMatchingTickets()
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(
                CreateTicket(Guid.NewGuid(), title: "Owned high", customerUserId: "customer-user", priority: TicketPriority.High),
                CreateTicket(Guid.NewGuid(), title: "Owned low", customerUserId: "customer-user", priority: TicketPriority.Low),
                CreateTicket(Guid.NewGuid(), title: "Other customer high", customerUserId: "other-customer", priority: TicketPriority.High));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Customer, userId: "customer-user");

        using var response = await client.GetAsync("/api/tickets?priority=High");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Single(body.Data.Items);
        Assert.Equal("Owned high", body.Data.Items[0].Title);
    }

    [Fact]
    public async Task GetTickets_WhenFilteringByPriority_ExcludesDeletedTickets()
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(
                CreateTicket(Guid.NewGuid(), title: "Visible urgent", priority: TicketPriority.Urgent),
                CreateTicket(Guid.NewGuid(), title: "Deleted urgent", priority: TicketPriority.Urgent, isDeleted: true));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.GetAsync("/api/tickets?priority=Urgent");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Single(body.Data.Items);
        Assert.Equal("Visible urgent", body.Data.Items[0].Title);
    }

    [Fact]
    public async Task GetTickets_WhenFilteringByStatusAndPriority_ReturnsOnlyTicketsMatchingBoth()
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(
                CreateTicket(Guid.NewGuid(), title: "Open high match", status: TicketStatus.Open, priority: TicketPriority.High),
                CreateTicket(Guid.NewGuid(), title: "Open low", status: TicketStatus.Open, priority: TicketPriority.Low),
                CreateTicket(Guid.NewGuid(), title: "Resolved high", status: TicketStatus.Resolved, priority: TicketPriority.High));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.GetAsync("/api/tickets?status=Open&priority=High");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Single(body.Data.Items);
        Assert.Equal("Open high match", body.Data.Items[0].Title);
    }

    [Fact]
    public async Task GetTickets_WhenPriorityIsInvalid_ReturnsBadRequest()
    {
        await using var factory = CreateFactory(_ => { });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.GetAsync("/api/tickets?priority=NotAPriority");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetTickets_WhenAdminFiltersByAssignedToUserId_ReturnsOnlyAssignedTickets()
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(
                CreateTicket(Guid.NewGuid(), title: "Assigned match", assignedToUserId: "agent-1"),
                CreateTicket(Guid.NewGuid(), title: "Other assignment", assignedToUserId: "agent-2"),
                CreateTicket(Guid.NewGuid(), title: "Unassigned"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.GetAsync("/api/tickets?assignedToUserId=agent-1");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Single(body.Data.Items);
        Assert.Equal("Assigned match", body.Data.Items[0].Title);
    }

    [Theory]
    [InlineData("status=Open", "Open assigned")]
    [InlineData("priority=High", "High assigned")]
    [InlineData("status=Open&priority=High", "Open high assigned")]
    public async Task GetTickets_WhenAdminCombinesAssignedToUserIdWithOtherFilters_ReturnsOnlyMatchingTickets(
        string additionalFilters,
        string expectedTitle)
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(
                CreateTicket(Guid.NewGuid(), title: "Open assigned", assignedToUserId: "agent-1", status: TicketStatus.Open, priority: TicketPriority.Low),
                CreateTicket(Guid.NewGuid(), title: "High assigned", assignedToUserId: "agent-1", status: TicketStatus.Resolved, priority: TicketPriority.High),
                CreateTicket(Guid.NewGuid(), title: "Open high assigned", assignedToUserId: "agent-1", status: TicketStatus.Open, priority: TicketPriority.High),
                CreateTicket(Guid.NewGuid(), title: "Other agent open high", assignedToUserId: "agent-2", status: TicketStatus.Open, priority: TicketPriority.High));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.GetAsync($"/api/tickets?assignedToUserId=agent-1&{additionalFilters}");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Contains(body.Data.Items, ticket => ticket.Title == expectedTitle);
        Assert.DoesNotContain(body.Data.Items, ticket => ticket.Title == "Other agent open high");

        if (additionalFilters == "status=Open&priority=High")
        {
            Assert.Single(body.Data.Items);
        }
    }

    [Fact]
    public async Task GetTickets_WhenAdminFiltersByAssignedToUserId_ExcludesDeletedTickets()
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(
                CreateTicket(Guid.NewGuid(), title: "Visible assigned", assignedToUserId: "agent-1"),
                CreateTicket(Guid.NewGuid(), title: "Deleted assigned", assignedToUserId: "agent-1", isDeleted: true));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.GetAsync("/api/tickets?assignedToUserId=agent-1");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Single(body.Data.Items);
        Assert.Equal("Visible assigned", body.Data.Items[0].Title);
    }

    [Fact]
    public async Task GetTickets_WhenAgentFiltersByOwnAssignedToUserId_Succeeds()
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(
                CreateTicket(Guid.NewGuid(), title: "Own assigned", assignedToUserId: "agent-user"),
                CreateTicket(Guid.NewGuid(), title: "Other assigned", assignedToUserId: "other-agent"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Agent, userId: "agent-user");

        using var response = await client.GetAsync("/api/tickets?assignedToUserId=agent-user");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Single(body.Data.Items);
        Assert.Equal("Own assigned", body.Data.Items[0].Title);
    }

    [Fact]
    public async Task GetTickets_WhenAgentFiltersByAnotherAssignedToUserId_ReturnsBadRequest()
    {
        await using var factory = CreateFactory(_ => { });
        using var client = CreateClient(factory, AuthRoles.Agent, userId: "agent-user");

        using var response = await client.GetAsync("/api/tickets?assignedToUserId=other-agent");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetTickets_WhenCustomerProvidesAssignedToUserId_ReturnsBadRequest()
    {
        await using var factory = CreateFactory(_ => { });
        using var client = CreateClient(factory, AuthRoles.Customer, userId: "customer-user");

        using var response = await client.GetAsync("/api/tickets?assignedToUserId=agent-user");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetTickets_WhenAssignedToUserIdIsEmpty_ReturnsBadRequest(string assignedToUserId)
    {
        await using var factory = CreateFactory(_ => { });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.GetAsync(
            $"/api/tickets?assignedToUserId={Uri.EscapeDataString(assignedToUserId)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetTickets_WhenAssignedToUserIdExceedsMaximumLength_ReturnsBadRequest()
    {
        await using var factory = CreateFactory(_ => { });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");
        var assignedToUserId = new string('a', 101);

        using var response = await client.GetAsync($"/api/tickets?assignedToUserId={assignedToUserId}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetTickets_WhenAdminFiltersUnassigned_ReturnsNullAndEmptyAssignments()
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(
                CreateTicket(Guid.NewGuid(), title: "Null assignment"),
                CreateTicket(Guid.NewGuid(), title: "Empty assignment", assignedToUserId: ""),
                CreateTicket(Guid.NewGuid(), title: "Assigned ticket", assignedToUserId: "agent-1"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.GetAsync("/api/tickets?unassigned=true");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Equal(2, body.Data.TotalCount);
        Assert.Contains(body.Data.Items, ticket => ticket.Title == "Null assignment");
        Assert.Contains(body.Data.Items, ticket => ticket.Title == "Empty assignment");
        Assert.DoesNotContain(body.Data.Items, ticket => ticket.Title == "Assigned ticket");
    }

    [Theory]
    [InlineData("status=Open", "Open unassigned")]
    [InlineData("priority=High", "High unassigned")]
    [InlineData("status=Open&priority=High", "Open high unassigned")]
    public async Task GetTickets_WhenAdminCombinesUnassignedWithOtherFilters_ReturnsOnlyMatchingTickets(
        string additionalFilters,
        string expectedTitle)
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(
                CreateTicket(Guid.NewGuid(), title: "Open unassigned", status: TicketStatus.Open, priority: TicketPriority.Low),
                CreateTicket(Guid.NewGuid(), title: "High unassigned", status: TicketStatus.Resolved, priority: TicketPriority.High),
                CreateTicket(Guid.NewGuid(), title: "Open high unassigned", status: TicketStatus.Open, priority: TicketPriority.High),
                CreateTicket(Guid.NewGuid(), title: "Assigned open high", assignedToUserId: "agent-1", status: TicketStatus.Open, priority: TicketPriority.High));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.GetAsync($"/api/tickets?unassigned=true&{additionalFilters}");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Contains(body.Data.Items, ticket => ticket.Title == expectedTitle);
        Assert.DoesNotContain(body.Data.Items, ticket => ticket.Title == "Assigned open high");

        if (additionalFilters == "status=Open&priority=High")
        {
            Assert.Single(body.Data.Items);
        }
    }

    [Fact]
    public async Task GetTickets_WhenAdminFiltersUnassigned_ExcludesDeletedTickets()
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(
                CreateTicket(Guid.NewGuid(), title: "Visible unassigned"),
                CreateTicket(Guid.NewGuid(), title: "Deleted unassigned", isDeleted: true));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.GetAsync("/api/tickets?unassigned=true");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Single(body.Data.Items);
        Assert.Equal("Visible unassigned", body.Data.Items[0].Title);
    }

    [Fact]
    public async Task GetTickets_WhenUnassignedIsFalse_PreservesExistingBehavior()
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(
                CreateTicket(Guid.NewGuid(), title: "Unassigned ticket"),
                CreateTicket(Guid.NewGuid(), title: "Assigned ticket", assignedToUserId: "agent-1"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.GetAsync("/api/tickets?unassigned=false");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Equal(2, body.Data.TotalCount);
    }

    [Theory]
    [InlineData(AuthRoles.Agent, "agent-user")]
    [InlineData(AuthRoles.Customer, "customer-user")]
    public async Task GetTickets_WhenNonAdminUsesUnassignedTrue_ReturnsBadRequest(string role, string userId)
    {
        await using var factory = CreateFactory(_ => { });
        using var client = CreateClient(factory, role, userId);

        using var response = await client.GetAsync("/api/tickets?unassigned=true");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetTickets_WhenAssignedToUserIdAndUnassignedTrueAreCombined_ReturnsBadRequest()
    {
        await using var factory = CreateFactory(_ => { });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.GetAsync("/api/tickets?assignedToUserId=agent-1&unassigned=true");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetTickets_WhenAssignedToUserIdAndUnassignedFalseAreCombined_AppliesAssignmentFilter()
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(
                CreateTicket(Guid.NewGuid(), title: "Assigned match", assignedToUserId: "agent-1"),
                CreateTicket(Guid.NewGuid(), title: "Other assigned", assignedToUserId: "agent-2"),
                CreateTicket(Guid.NewGuid(), title: "Unassigned"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.GetAsync("/api/tickets?assignedToUserId=agent-1&unassigned=false");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Single(body.Data.Items);
        Assert.Equal("Assigned match", body.Data.Items[0].Title);
    }

    [Fact]
    public async Task GetTickets_WhenUnassignedIsInvalid_ReturnsBadRequest()
    {
        await using var factory = CreateFactory(_ => { });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.GetAsync("/api/tickets?unassigned=not-a-boolean");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("payment", "Payment page failure")]
    [InlineData("PASSWORD", "Login issue")]
    [InlineData("%20PaYmEnT%20", "Payment page failure")]
    public async Task GetTickets_WhenAdminSearches_MatchesTitleOrDescriptionCaseInsensitively(
        string search,
        string expectedTitle)
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(
                CreateTicket(Guid.NewGuid(), title: "Payment page failure"),
                CreateTicket(Guid.NewGuid(), title: "Login issue", description: "Customer cannot reset their password."),
                CreateTicket(Guid.NewGuid(), title: "Unrelated issue"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.GetAsync($"/api/tickets?search={search}");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Single(body.Data.Items);
        Assert.Equal(expectedTitle, body.Data.Items[0].Title);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetTickets_WhenSearchIsEmpty_ReturnsBadRequest(string search)
    {
        await using var factory = CreateFactory(_ => { });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.GetAsync($"/api/tickets?search={Uri.EscapeDataString(search)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetTickets_WhenSearchExceedsMaximumLength_ReturnsBadRequest()
    {
        await using var factory = CreateFactory(_ => { });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");
        var search = new string('a', 101);

        using var response = await client.GetAsync($"/api/tickets?search={search}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("status=Open", "Open payment")]
    [InlineData("priority=High", "High payment")]
    [InlineData("assignedToUserId=agent-1", "Assigned payment")]
    [InlineData("unassigned=true", "Unassigned payment")]
    public async Task GetTickets_WhenAdminCombinesSearchWithExistingFilter_ReturnsOnlyMatchingTickets(
        string additionalFilter,
        string expectedTitle)
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(
                CreateTicket(Guid.NewGuid(), title: "Open payment", status: TicketStatus.Open, priority: TicketPriority.Low, assignedToUserId: "agent-2"),
                CreateTicket(Guid.NewGuid(), title: "High payment", status: TicketStatus.Resolved, priority: TicketPriority.High, assignedToUserId: "agent-2"),
                CreateTicket(Guid.NewGuid(), title: "Assigned payment", status: TicketStatus.Resolved, priority: TicketPriority.Low, assignedToUserId: "agent-1"),
                CreateTicket(Guid.NewGuid(), title: "Unassigned payment", status: TicketStatus.Resolved, priority: TicketPriority.Low),
                CreateTicket(Guid.NewGuid(), title: "Open unrelated", status: TicketStatus.Open, priority: TicketPriority.High, assignedToUserId: "agent-1"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.GetAsync($"/api/tickets?search=payment&{additionalFilter}");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Single(body.Data.Items);
        Assert.Equal(expectedTitle, body.Data.Items[0].Title);
    }

    [Fact]
    public async Task GetTickets_WhenAgentSearches_ReturnsOnlyAssignedMatchingTickets()
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(
                CreateTicket(Guid.NewGuid(), title: "Own payment issue", assignedToUserId: "agent-user"),
                CreateTicket(Guid.NewGuid(), title: "Other payment issue", assignedToUserId: "other-agent"),
                CreateTicket(Guid.NewGuid(), title: "Own unrelated issue", assignedToUserId: "agent-user"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Agent, userId: "agent-user");

        using var response = await client.GetAsync("/api/tickets?search=payment");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Single(body.Data.Items);
        Assert.Equal("Own payment issue", body.Data.Items[0].Title);
    }

    [Fact]
    public async Task GetTickets_WhenCustomerSearches_ReturnsOnlyOwnedMatchingTickets()
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(
                CreateTicket(Guid.NewGuid(), title: "Owned payment issue", customerUserId: "customer-user"),
                CreateTicket(Guid.NewGuid(), title: "Other payment issue", customerUserId: "other-customer"),
                CreateTicket(Guid.NewGuid(), title: "Owned unrelated issue", customerUserId: "customer-user"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Customer, userId: "customer-user");

        using var response = await client.GetAsync("/api/tickets?search=payment");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Single(body.Data.Items);
        Assert.Equal("Owned payment issue", body.Data.Items[0].Title);
    }

    [Fact]
    public async Task GetTickets_WhenSearching_ExcludesDeletedTickets()
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(
                CreateTicket(Guid.NewGuid(), title: "Visible payment issue"),
                CreateTicket(Guid.NewGuid(), title: "Deleted payment issue", isDeleted: true));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.GetAsync("/api/tickets?search=payment");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Single(body.Data.Items);
        Assert.Equal("Visible payment issue", body.Data.Items[0].Title);
    }

    [Fact]
    public async Task GetTickets_WhenPaginationIsOmitted_UsesDefaultPageAndPageSize()
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(Enumerable.Range(1, 21)
                .Select(index => CreateTicket(Guid.NewGuid(), title: $"Ticket {index}")));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.GetAsync("/api/tickets");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Equal(20, body.Data.Items.Count);
        Assert.Equal(1, body.Data.Page);
        Assert.Equal(20, body.Data.PageSize);
        Assert.Equal(21, body.Data.TotalCount);
        Assert.Equal(2, body.Data.TotalPages);
        Assert.False(body.Data.HasPreviousPage);
        Assert.True(body.Data.HasNextPage);
    }

    [Fact]
    public async Task GetTickets_WhenPageIsRequested_ReturnsRequestedPageAndMetadata()
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(Enumerable.Range(1, 5)
                .Select(index => CreateTicket(
                    Guid.NewGuid(),
                    title: $"Ticket {index}",
                    createdAtUtc: DateTime.UtcNow.AddMinutes(index))));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.GetAsync("/api/tickets?page=2&pageSize=2");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Equal(2, body.Data.Items.Count);
        Assert.Equal(["Ticket 3", "Ticket 2"], body.Data.Items.Select(ticket => ticket.Title));
        Assert.Equal(2, body.Data.Page);
        Assert.Equal(2, body.Data.PageSize);
        Assert.Equal(5, body.Data.TotalCount);
        Assert.Equal(3, body.Data.TotalPages);
        Assert.True(body.Data.HasPreviousPage);
        Assert.True(body.Data.HasNextPage);
    }

    [Fact]
    public async Task GetTickets_WhenPageIsBeyondTotalPages_ReturnsEmptyItemsWithMetadata()
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(
                CreateTicket(Guid.NewGuid(), title: "Ticket one"),
                CreateTicket(Guid.NewGuid(), title: "Ticket two"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.GetAsync("/api/tickets?page=3&pageSize=1");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Empty(body.Data.Items);
        Assert.Equal(3, body.Data.Page);
        Assert.Equal(1, body.Data.PageSize);
        Assert.Equal(2, body.Data.TotalCount);
        Assert.Equal(2, body.Data.TotalPages);
        Assert.True(body.Data.HasPreviousPage);
        Assert.False(body.Data.HasNextPage);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=-1")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=-1")]
    [InlineData("pageSize=101")]
    [InlineData("page=not-a-number")]
    [InlineData("pageSize=not-a-number")]
    public async Task GetTickets_WhenPaginationIsInvalid_ReturnsBadRequest(string query)
    {
        await using var factory = CreateFactory(_ => { });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.GetAsync($"/api/tickets?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetTickets_WhenPageSizeIsMaximum_ReturnsOk()
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(Guid.NewGuid()));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.GetAsync("/api/tickets?pageSize=100");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Equal(100, body.Data.PageSize);
    }

    [Fact]
    public async Task GetTickets_WhenPageIsVeryLarge_ReturnsEmptyItems()
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(Guid.NewGuid()));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.GetAsync($"/api/tickets?page={int.MaxValue}&pageSize=100");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Empty(body.Data.Items);
        Assert.Equal(1, body.Data.TotalCount);
        Assert.Equal(1, body.Data.TotalPages);
    }

    [Theory]
    [InlineData("search=payment")]
    [InlineData("status=Open")]
    [InlineData("priority=High")]
    [InlineData("assignedToUserId=agent-1")]
    public async Task GetTickets_WhenAdminPaginatesFilteredTickets_TotalCountUsesAllMatches(string filter)
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(
                CreateTicket(Guid.NewGuid(), title: "Payment one", assignedToUserId: "agent-1", status: TicketStatus.Open, priority: TicketPriority.High),
                CreateTicket(Guid.NewGuid(), title: "Payment two", assignedToUserId: "agent-1", status: TicketStatus.Open, priority: TicketPriority.High),
                CreateTicket(Guid.NewGuid(), title: "Payment three", assignedToUserId: "agent-1", status: TicketStatus.Open, priority: TicketPriority.High),
                CreateTicket(Guid.NewGuid(), title: "Unrelated", assignedToUserId: "agent-2", status: TicketStatus.Resolved, priority: TicketPriority.Low));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.GetAsync($"/api/tickets?{filter}&page=2&pageSize=1");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Single(body.Data.Items);
        Assert.Equal(3, body.Data.TotalCount);
        Assert.Equal(3, body.Data.TotalPages);
    }

    [Fact]
    public async Task GetTickets_WhenAdminPaginatesUnassignedTickets_TotalCountUsesAllMatches()
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(
                CreateTicket(Guid.NewGuid(), title: "Unassigned one"),
                CreateTicket(Guid.NewGuid(), title: "Unassigned two"),
                CreateTicket(Guid.NewGuid(), title: "Unassigned three"),
                CreateTicket(Guid.NewGuid(), title: "Assigned", assignedToUserId: "agent-1"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.GetAsync("/api/tickets?unassigned=true&page=2&pageSize=1");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Single(body.Data.Items);
        Assert.Equal(3, body.Data.TotalCount);
        Assert.Equal(3, body.Data.TotalPages);
    }

    [Theory]
    [InlineData(AuthRoles.Agent, "agent-user")]
    [InlineData(AuthRoles.Customer, "customer-user")]
    public async Task GetTickets_WhenRoleScopedUserPaginates_TotalCountUsesOnlyVisibleTickets(
        string role,
        string userId)
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(
                CreateTicket(Guid.NewGuid(), title: "Visible one", assignedToUserId: "agent-user", customerUserId: "customer-user"),
                CreateTicket(Guid.NewGuid(), title: "Visible two", assignedToUserId: "agent-user", customerUserId: "customer-user"),
                CreateTicket(Guid.NewGuid(), title: "Visible three", assignedToUserId: "agent-user", customerUserId: "customer-user"),
                CreateTicket(Guid.NewGuid(), title: "Other user ticket", assignedToUserId: "other-agent", customerUserId: "other-customer"),
                CreateTicket(Guid.NewGuid(), title: "Deleted visible ticket", assignedToUserId: "agent-user", customerUserId: "customer-user", isDeleted: true));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, role, userId);

        using var response = await client.GetAsync("/api/tickets?page=2&pageSize=2");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Single(body.Data.Items);
        Assert.Equal(3, body.Data.TotalCount);
        Assert.Equal(2, body.Data.TotalPages);
    }

    [Theory]
    [InlineData("createdAt", "asc", "Oldest")]
    [InlineData("createdAt", "desc", "Newest")]
    [InlineData("CrEaTeDaT", "AsC", "Oldest")]
    [InlineData("%20createdAt%20", "%20desc%20", "Newest")]
    public async Task GetTickets_WhenSortingByCreatedAt_ReturnsExpectedOrder(
        string sortBy,
        string sortDirection,
        string expectedFirstTitle)
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(
                CreateTicket(Guid.NewGuid(), title: "Oldest", createdAtUtc: DateTime.UtcNow.AddDays(-2)),
                CreateTicket(Guid.NewGuid(), title: "Middle", createdAtUtc: DateTime.UtcNow.AddDays(-1)),
                CreateTicket(Guid.NewGuid(), title: "Newest", createdAtUtc: DateTime.UtcNow));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.GetAsync(
            $"/api/tickets?sortBy={sortBy}&sortDirection={sortDirection}");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Equal(expectedFirstTitle, body.Data.Items[0].Title);
    }

    [Theory]
    [InlineData("asc", "Old update")]
    [InlineData("desc", "New update")]
    public async Task GetTickets_WhenSortingByUpdatedAt_ReturnsExpectedOrder(
        string sortDirection,
        string expectedFirstTitle)
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(
                CreateTicket(Guid.NewGuid(), title: "Old update", updatedAtUtc: DateTime.UtcNow.AddDays(-2)),
                CreateTicket(Guid.NewGuid(), title: "New update", updatedAtUtc: DateTime.UtcNow));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.GetAsync(
            $"/api/tickets?sortBy=updatedAt&sortDirection={sortDirection}");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Equal(expectedFirstTitle, body.Data.Items[0].Title);
    }

    [Theory]
    [InlineData("priority", "asc", "Low")]
    [InlineData("priority", "desc", "Urgent")]
    [InlineData("status", "asc", "Open")]
    [InlineData("status", "desc", "Closed")]
    public async Task GetTickets_WhenSortingByEnumWorkflowOrder_ReturnsExpectedOrder(
        string sortBy,
        string sortDirection,
        string expectedFirstTitle)
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(
                CreateTicket(Guid.NewGuid(), title: "Low", priority: TicketPriority.Low, status: TicketStatus.InProgress),
                CreateTicket(Guid.NewGuid(), title: "Urgent", priority: TicketPriority.Urgent, status: TicketStatus.Resolved),
                CreateTicket(Guid.NewGuid(), title: "Open", priority: TicketPriority.Medium, status: TicketStatus.Open),
                CreateTicket(Guid.NewGuid(), title: "Closed", priority: TicketPriority.High, status: TicketStatus.Closed));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.GetAsync(
            $"/api/tickets?sortBy={sortBy}&sortDirection={sortDirection}");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Equal(expectedFirstTitle, body.Data.Items[0].Title);
    }

    [Fact]
    public async Task GetTickets_WhenSortDirectionIsOmitted_DefaultsToDescending()
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(
                CreateTicket(Guid.NewGuid(), title: "Low", priority: TicketPriority.Low),
                CreateTicket(Guid.NewGuid(), title: "Urgent", priority: TicketPriority.Urgent));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.GetAsync("/api/tickets?sortBy=priority");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Equal("Urgent", body.Data.Items[0].Title);
    }

    [Theory]
    [InlineData("sortBy=title")]
    [InlineData("sortDirection=sideways")]
    [InlineData("sortBy=")]
    [InlineData("sortBy=%20%20%20")]
    [InlineData("sortDirection=")]
    [InlineData("sortDirection=%20%20%20")]
    public async Task GetTickets_WhenSortingIsInvalid_ReturnsBadRequest(string query)
    {
        await using var factory = CreateFactory(_ => { });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.GetAsync($"/api/tickets?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetTickets_WhenSortingAndPaginating_AppliesSortingBeforePagination()
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(
                CreateTicket(Guid.NewGuid(), title: "Low", priority: TicketPriority.Low),
                CreateTicket(Guid.NewGuid(), title: "Medium", priority: TicketPriority.Medium),
                CreateTicket(Guid.NewGuid(), title: "High", priority: TicketPriority.High),
                CreateTicket(Guid.NewGuid(), title: "Urgent", priority: TicketPriority.Urgent));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.GetAsync(
            "/api/tickets?sortBy=priority&sortDirection=desc&page=2&pageSize=1");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Single(body.Data.Items);
        Assert.Equal("High", body.Data.Items[0].Title);
        Assert.Equal(4, body.Data.TotalCount);
        Assert.Equal(4, body.Data.TotalPages);
    }

    [Theory]
    [InlineData("search=payment")]
    [InlineData("status=Open&priority=High")]
    [InlineData("assignedToUserId=agent-1")]
    [InlineData("unassigned=true")]
    public async Task GetTickets_WhenAdminSortsWithExistingFilters_ReturnsOnlyFilteredTicketsInOrder(string filter)
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(
                CreateTicket(Guid.NewGuid(), title: "Older payment", assignedToUserId: filter == "unassigned=true" ? null : "agent-1", status: TicketStatus.Open, priority: TicketPriority.High, createdAtUtc: DateTime.UtcNow.AddDays(-1)),
                CreateTicket(Guid.NewGuid(), title: "Newer payment", assignedToUserId: filter == "unassigned=true" ? null : "agent-1", status: TicketStatus.Open, priority: TicketPriority.High, createdAtUtc: DateTime.UtcNow),
                CreateTicket(Guid.NewGuid(), title: "Excluded", assignedToUserId: "agent-2", status: TicketStatus.Resolved, priority: TicketPriority.Low, createdAtUtc: DateTime.UtcNow.AddDays(1)));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.GetAsync(
            $"/api/tickets?{filter}&sortBy=createdAt&sortDirection=asc");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Equal(2, body.Data.Items.Count);
        Assert.Equal(["Older payment", "Newer payment"], body.Data.Items.Select(ticket => ticket.Title));
    }

    [Theory]
    [InlineData(AuthRoles.Agent, "agent-user")]
    [InlineData(AuthRoles.Customer, "customer-user")]
    public async Task GetTickets_WhenRoleScopedUserSorts_RemainsScopedToVisibleTickets(string role, string userId)
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.AddRange(
                CreateTicket(Guid.NewGuid(), title: "Visible low", assignedToUserId: "agent-user", customerUserId: "customer-user", priority: TicketPriority.Low),
                CreateTicket(Guid.NewGuid(), title: "Visible urgent", assignedToUserId: "agent-user", customerUserId: "customer-user", priority: TicketPriority.Urgent),
                CreateTicket(Guid.NewGuid(), title: "Other urgent", assignedToUserId: "other-agent", customerUserId: "other-customer", priority: TicketPriority.Urgent),
                CreateTicket(Guid.NewGuid(), title: "Deleted urgent", assignedToUserId: "agent-user", customerUserId: "customer-user", priority: TicketPriority.Urgent, isDeleted: true));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, role, userId);

        using var response = await client.GetAsync(
            "/api/tickets?sortBy=priority&sortDirection=desc");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body?.Data);
        Assert.Equal(2, body.Data.Items.Count);
        Assert.Equal(["Visible urgent", "Visible low"], body.Data.Items.Select(ticket => ticket.Title));
    }

    [Fact]
    public async Task GetTicketById_WithoutToken_ReturnsUnauthorized()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(ticketId));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, role: null);

        using var response = await client.GetAsync($"/api/tickets/{ticketId}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetTicketById_WhenAdmin_CanAccessAnyTicket()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(ticketId, title: "Any ticket"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.GetAsync($"/api/tickets/{ticketId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetTicketById_WhenAgent_CanAccessAssignedTicket()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(ticketId, assignedToUserId: "agent-user"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Agent, userId: "agent-user");

        using var response = await client.GetAsync($"/api/tickets/{ticketId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetTicketById_WhenAgentIsNotAssigned_ReturnsNotFound()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(ticketId, assignedToUserId: "other-agent"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Agent, userId: "agent-user");

        using var response = await client.GetAsync($"/api/tickets/{ticketId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetTicketById_WhenCustomer_CanAccessOwnedTicket()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(ticketId, customerUserId: "customer-user"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Customer, userId: "customer-user");

        using var response = await client.GetAsync($"/api/tickets/{ticketId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetTicketById_WhenCustomerDoesNotOwnTicket_ReturnsNotFound()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(ticketId, customerUserId: "other-customer"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Customer, userId: "customer-user");

        using var response = await client.GetAsync($"/api/tickets/{ticketId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetTicketById_WhenTicketIsDeleted_ReturnsNotFound()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(ticketId, isDeleted: true));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await client.GetAsync($"/api/tickets/{ticketId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddMessage_WithoutToken_ReturnsUnauthorized()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(ticketId));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, role: null);

        using var response = await PostMessageAsync(client, ticketId, isInternalNote: false);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AddMessage_WhenAdmin_AddsNormalMessage()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(ticketId, customerUserId: "customer-user"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await PostMessageAsync(client, ticketId, isInternalNote: false);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task AddMessage_WhenAdmin_AddsInternalNote()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(ticketId));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await PostMessageAsync(client, ticketId, isInternalNote: true);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task AddMessage_WhenAgentIsAssigned_AddsMessage()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(ticketId, assignedToUserId: "agent-user"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Agent, userId: "agent-user");

        using var response = await PostMessageAsync(client, ticketId, isInternalNote: false);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task AddMessage_WhenAgentIsAssigned_AddsInternalNote()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(ticketId, assignedToUserId: "agent-user"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Agent, userId: "agent-user");

        using var response = await PostMessageAsync(client, ticketId, isInternalNote: true);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task AddMessage_WhenAgentIsNotAssigned_ReturnsNotFound()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(ticketId, assignedToUserId: "other-agent"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Agent, userId: "agent-user");

        using var response = await PostMessageAsync(client, ticketId, isInternalNote: false);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddMessage_WhenCustomerOwnsTicket_AddsMessage()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(ticketId, customerUserId: "customer-user"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Customer, userId: "customer-user");

        using var response = await PostMessageAsync(client, ticketId, isInternalNote: false);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task AddMessage_WhenCustomerDoesNotOwnTicket_ReturnsNotFound()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(ticketId, customerUserId: "other-customer"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Customer, userId: "customer-user");

        using var response = await PostMessageAsync(client, ticketId, isInternalNote: false);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddMessage_WhenCustomerAddsInternalNote_ReturnsBadRequest()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(ticketId, customerUserId: "customer-user"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Customer, userId: "customer-user");

        using var response = await PostMessageAsync(client, ticketId, isInternalNote: true);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Customers cannot add internal notes.", content);
    }

    [Fact]
    public async Task AddMessage_BetweenCustomerAndAgent_CreatesRetrievableNotifications()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(
                ticketId,
                title: "Conversation notification ticket",
                assignedToUserId: "agent-user",
                customerUserId: "customer-user"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Customer, userId: "customer-user");

        using var customerMessageResponse = await PostMessageAsync(client, ticketId, isInternalNote: false);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken(AuthRoles.Agent, "agent-user"));
        using var agentNotificationsResponse = await client.GetAsync("/api/notifications");
        var agentNotifications = await agentNotificationsResponse.Content.ReadAsStringAsync();
        using var agentMessageResponse = await PostMessageAsync(client, ticketId, isInternalNote: false);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken(AuthRoles.Customer, "customer-user"));
        using var customerNotificationsResponse = await client.GetAsync("/api/notifications");
        var customerNotifications = await customerNotificationsResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, customerMessageResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, agentNotificationsResponse.StatusCode);
        Assert.Contains("\"type\":\"TicketMessageCreated\"", agentNotifications);
        Assert.Contains(ticketId.ToString(), agentNotifications);
        Assert.Equal(HttpStatusCode.Created, agentMessageResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, customerNotificationsResponse.StatusCode);
        Assert.Contains("\"type\":\"TicketMessageCreated\"", customerNotifications);
        Assert.Contains(ticketId.ToString(), customerNotifications);
    }

    [Fact]
    public async Task ChangeStatus_WithoutToken_ReturnsUnauthorized()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(ticketId));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, role: null);

        using var response = await PatchStatusAsync(client, ticketId);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_WithCustomerToken_ReturnsForbidden()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(ticketId, customerUserId: "customer-user"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Customer, userId: "customer-user");

        using var response = await PatchStatusAsync(client, ticketId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_WhenAdmin_ChangesAnyTicket()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(ticketId, assignedToUserId: "other-agent"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await PatchStatusAsync(client, ticketId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_WhenAgentIsAssigned_ChangesStatus()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(ticketId, assignedToUserId: "agent-user"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Agent, userId: "agent-user");

        using var response = await PatchStatusAsync(client, ticketId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_WhenAgentChangesCustomerOwnedTicket_CustomerCanRetrieveNotification()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(
                ticketId,
                title: "Customer notification ticket",
                assignedToUserId: "agent-user",
                customerUserId: "customer-user"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Agent, userId: "agent-user");

        using var statusResponse = await PatchStatusAsync(client, ticketId);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken(AuthRoles.Customer, "customer-user"));
        using var notificationsResponse = await client.GetAsync("/api/notifications");
        var content = await notificationsResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, notificationsResponse.StatusCode);
        Assert.Contains("\"type\":\"TicketStatusChanged\"", content);
        Assert.Contains("\"title\":\"Ticket status updated\"", content);
        Assert.Contains(ticketId.ToString(), content);
    }

    [Fact]
    public async Task ChangeStatus_WhenAgentIsNotAssigned_ReturnsNotFound()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(ticketId, assignedToUserId: "other-agent"));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Agent, userId: "agent-user");

        using var response = await PatchStatusAsync(client, ticketId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_WhenTicketIsDeleted_ReturnsNotFound()
    {
        var ticketId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Tickets.Add(CreateTicket(ticketId, assignedToUserId: "agent-user", isDeleted: true));
            dbContext.SaveChanges();
        });
        using var client = CreateClient(factory, AuthRoles.Admin, userId: "admin-user");

        using var response = await PatchStatusAsync(client, ticketId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory, string? role, string? userId = null)
    {
        var client = factory.CreateClient();
        if (role is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(role, userId));
        }

        return client;
    }

    private static Task<HttpResponseMessage> PostMessageAsync(HttpClient client, Guid ticketId, bool isInternalNote) =>
        client.PostAsJsonAsync($"/api/tickets/{ticketId}/messages", new
        {
            message = isInternalNote ? "Internal note from authorization test." : "Message from authorization test.",
            isInternalNote,
            createdByUserId = "request-user",
            createdByDisplayName = "Request User"
        });

    private static Task<HttpResponseMessage> PostInternalNoteAsync(HttpClient client, Guid ticketId, string body) =>
        client.PostAsJsonAsync($"/api/tickets/{ticketId}/internal-notes", new { body });

    private static Task<HttpResponseMessage> PatchStatusAsync(HttpClient client, Guid ticketId) =>
        client.PatchAsJsonAsync($"/api/tickets/{ticketId}/status", new
        {
            status = "InProgress",
            changedByUserId = "request-user",
            changedByDisplayName = "Request User"
        });

    private static Dictionary<string, string?> CreateRateLimitConfiguration(int permitLimit) =>
        new()
        {
            ["AiAssistant:Provider"] = AiAssistantProviders.RuleBased,
            ["AiAssistant:RateLimit:PermitLimit"] = permitLimit.ToString(),
            ["AiAssistant:RateLimit:WindowSeconds"] = "60"
        };

    private static WebApplicationFactory<Program> CreateFactory(
        Action<ApplicationDbContext> seed,
        Dictionary<string, string?>? configuration = null,
        IAiOperationTelemetry? telemetry = null)
    {
        var databaseName = Guid.NewGuid().ToString();

        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                if (configuration is not null)
                {
                    builder.ConfigureAppConfiguration((_, configBuilder) =>
                    {
                        configBuilder.AddInMemoryCollection(configuration);
                    });
                }

                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                    services.RemoveAll<IAiTicketAssistantService>();

                    services.AddDbContext<ApplicationDbContext>(options =>
                        options.UseInMemoryDatabase(databaseName));

                    services.AddScoped<IAiTicketAssistantService, TestAiTicketAssistantService>();
                    if (telemetry is not null)
                    {
                        services.RemoveAll<IAiOperationTelemetry>();
                        services.AddSingleton<IAiOperationTelemetry>(telemetry);
                    }

                    using var serviceProvider = services.BuildServiceProvider();
                    using var scope = serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    seed(dbContext);
                });
            });
    }

    private static Ticket CreateTicket(
        Guid id,
        string title = "Authorization ticket",
        string? assignedToUserId = null,
        string? customerUserId = null,
        TicketStatus status = TicketStatus.Open,
        TicketPriority priority = TicketPriority.Medium,
        bool isDeleted = false,
        string description = "Ticket for authorization tests.",
        DateTime? createdAtUtc = null,
        DateTime? updatedAtUtc = null) =>
        new()
        {
            Id = id,
            Title = title,
            Description = description,
            Status = status,
            Priority = priority,
            Category = TicketCategory.General,
            Source = TicketSource.Web,
            AssignedToUserId = assignedToUserId,
            CustomerUserId = customerUserId,
            CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow,
            UpdatedAtUtc = updatedAtUtc,
            IsDeleted = isDeleted
        };

    private static void SeedAgent(ApplicationDbContext dbContext, string userId) =>
        SeedUserInRole(dbContext, userId, AuthRoles.Agent);

    private static void SeedCustomer(ApplicationDbContext dbContext, string userId) =>
        SeedUserInRole(dbContext, userId, AuthRoles.Customer);

    private static void SeedUserInRole(ApplicationDbContext dbContext, string userId, string roleName)
    {
        var role = dbContext.Roles.SingleOrDefault(role => role.NormalizedName == roleName.ToUpperInvariant());
        if (role is null)
        {
            role = new IdentityRole
            {
                Id = $"{roleName.ToLowerInvariant()}-role",
                Name = roleName,
                NormalizedName = roleName.ToUpperInvariant()
            };
            dbContext.Roles.Add(role);
        }

        dbContext.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = $"{userId}@example.com",
            NormalizedUserName = $"{userId}@example.com".ToUpperInvariant(),
            Email = $"{userId}@example.com",
            NormalizedEmail = $"{userId}@example.com".ToUpperInvariant(),
            EmailConfirmed = true,
            FullName = userId,
            CreatedAtUtc = DateTime.UtcNow,
            IsActive = true
        });
        dbContext.UserRoles.Add(new IdentityUserRole<string>
        {
            UserId = userId,
            RoleId = role.Id
        });
    }

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

    private sealed class TestAiTicketAssistantService(
        IAiOperationTelemetryContext telemetryContext) : IAiTicketAssistantService
    {
        public Task<TicketAssistantResult> AnalyzeAsync(TicketAssistantRequest request, CancellationToken cancellationToken = default)
        {
            telemetryContext.MarkProvider(AiAssistantProviders.RuleBased);
            return Task.FromResult(new TicketAssistantResult(
                "Summary",
                "General",
                "Medium",
                "Reply",
                [],
                "Test"));
        }

        public Task<string> SuggestReplyAsync(
            TicketReplySuggestionRequest request,
            CancellationToken cancellationToken = default)
        {
            telemetryContext.MarkProvider(AiAssistantProviders.RuleBased);
            return Task.FromResult("Suggested reply from authorization test AI.");
        }

        public Task<string> SummarizeTicketAsync(
            TicketSummaryRequest request,
            CancellationToken cancellationToken = default)
        {
            telemetryContext.MarkProvider(AiAssistantProviders.RuleBased);
            return Task.FromResult("Ticket summary from authorization test AI.");
        }

        public Task<TicketTriageSuggestion> SuggestTriageAsync(
            TicketTriageRequest request,
            CancellationToken cancellationToken = default)
        {
            telemetryContext.MarkProvider(AiAssistantProviders.RuleBased);
            return Task.FromResult(new TicketTriageSuggestion(
                TicketPriority.High,
                TicketCategory.Billing,
                true,
                "The customer reports repeated payment failures.",
                "The issue blocks checkout and may need faster support review."));
        }
    }

    private sealed class TestAiOperationTelemetry : IAiOperationTelemetry
    {
        private readonly object gate = new();

        public List<AiOperationTelemetryRecord> Records { get; } = [];

        public Task RecordAsync(AiOperationTelemetryRecord record, CancellationToken cancellationToken = default)
        {
            lock (gate)
            {
                Records.Add(record);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingAiOperationTelemetry : IAiOperationTelemetry
    {
        public Task RecordAsync(AiOperationTelemetryRecord record, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Telemetry sink failed.");
    }
}

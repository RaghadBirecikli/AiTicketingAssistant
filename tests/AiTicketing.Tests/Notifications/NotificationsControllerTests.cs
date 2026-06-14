using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AiTicketing.Application.Auth;
using AiTicketing.Domain.Entities;
using AiTicketing.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace AiTicketing.Tests.Notifications;

public sealed class NotificationsControllerTests
{
    private const string Issuer = "AiTicketingAssistant";
    private const string Audience = "AiTicketingAssistant";
    private const string SecretKey = "REPLACE_WITH_A_PRODUCTION_SECRET_KEY_AT_LEAST_32_BYTES";

    [Fact]
    public async Task Swagger_DocumentsNotificationsApiContract()
    {
        await using var factory = CreateFactory(environment: "Development");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/swagger/v1/swagger.json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var paths = document.RootElement.GetProperty("paths");
        AssertResponseCodes(paths.GetProperty("/api/notifications").GetProperty("get"), "200", "401");
        AssertResponseCodes(paths.GetProperty("/api/notifications/unread-count").GetProperty("get"), "200", "401");
        AssertResponseCodes(paths.GetProperty("/api/notifications/{id}/read").GetProperty("patch"), "200", "401", "404");
        AssertResponseCodes(paths.GetProperty("/api/notifications/read-all").GetProperty("patch"), "200", "401");

        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        AssertSchemaProperties(
            schemas.GetProperty("NotificationResponse"),
            "id",
            "title",
            "message",
            "type",
            "ticketId",
            "isRead",
            "createdAtUtc",
            "readAtUtc");
        AssertSchemaProperties(schemas.GetProperty("UnreadNotificationCountResponse"), "unreadCount");
        AssertSchemaProperties(schemas.GetProperty("MarkAllNotificationsAsReadResponse"), "updatedCount");
    }

    [Fact]
    public async Task GetNotifications_WithoutToken_ReturnsUnauthorized()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/notifications");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetNotifications_WithToken_ReturnsOnlyCurrentUserNotifications()
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Notifications.AddRange(
                CreateNotification("user-1", "Own notification"),
                CreateNotification("user-2", "Other notification"));
            dbContext.SaveChanges();
        });
        using var client = factory.CreateClient();
        AddToken(client, "user-1");

        using var response = await client.GetAsync("/api/notifications");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"success\":true", content);
        Assert.Contains("Own notification", content);
        Assert.DoesNotContain("Other notification", content);
    }

    [Fact]
    public async Task GetNotifications_ReturnsReadStateAndTimestampFields()
    {
        var notificationId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var readAtUtc = DateTime.UtcNow.AddMinutes(-5);
        await using var factory = CreateFactory(dbContext =>
        {
            var notification = CreateNotification("user-1", "Read notification", notificationId);
            notification.TicketId = ticketId;
            notification.IsRead = true;
            notification.ReadAtUtc = readAtUtc;
            dbContext.Notifications.Add(notification);
            dbContext.SaveChanges();
        });
        using var client = factory.CreateClient();
        AddToken(client, "user-1");

        using var response = await client.GetAsync("/api/notifications");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"\"id\":\"{notificationId}\"", content);
        Assert.Contains("\"title\":\"Read notification\"", content);
        Assert.Contains("\"message\":\"Read notification message\"", content);
        Assert.Contains("\"type\":\"Test\"", content);
        Assert.Contains($"\"ticketId\":\"{ticketId}\"", content);
        Assert.Contains("\"isRead\":true", content);
        Assert.Contains("\"createdAtUtc\":", content);
        Assert.Contains("\"readAtUtc\":", content);
        Assert.Contains("+00:00", content);
    }

    [Fact]
    public async Task GetUnreadCount_WithoutToken_ReturnsUnauthorized()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/notifications/unread-count");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetUnreadCount_ReturnsOnlyCurrentUsersUnreadNotificationCount()
    {
        await using var factory = CreateFactory(dbContext =>
        {
            var ownRead = CreateNotification("user-1", "Own read");
            ownRead.IsRead = true;
            ownRead.ReadAtUtc = DateTime.UtcNow;
            dbContext.Notifications.AddRange(
                CreateNotification("user-1", "Own unread one"),
                CreateNotification("user-1", "Own unread two"),
                ownRead,
                CreateNotification("user-2", "Other unread"));
            dbContext.SaveChanges();
        });
        using var client = factory.CreateClient();
        AddToken(client, "user-1");

        using var response = await client.GetAsync("/api/notifications/unread-count");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"unreadCount\":2", content);
    }

    [Fact]
    public async Task GetUnreadCount_WithAdminToken_ReturnsOnlyAdminsUnreadNotificationCount()
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Notifications.AddRange(
                CreateNotification("admin-user", "Admin unread"),
                CreateNotification("user-2", "Other unread one"),
                CreateNotification("user-2", "Other unread two"));
            dbContext.SaveChanges();
        });
        using var client = factory.CreateClient();
        AddToken(client, "admin-user", AuthRoles.Admin);

        using var response = await client.GetAsync("/api/notifications/unread-count");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"unreadCount\":1", content);
    }

    [Fact]
    public async Task GetUnreadCount_WhenUserHasNoUnreadNotifications_ReturnsZero()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        AddToken(client, "user-1");

        using var response = await client.GetAsync("/api/notifications/unread-count");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"unreadCount\":0", content);
    }

    [Fact]
    public async Task MarkAsRead_WithoutToken_ReturnsUnauthorized()
    {
        var notificationId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Notifications.Add(CreateNotification("user-1", "Unread notification", notificationId));
            dbContext.SaveChanges();
        });
        using var client = factory.CreateClient();

        using var response = await client.PatchAsync($"/api/notifications/{notificationId}/read", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MarkAsRead_WithOwnerToken_ReturnsOk()
    {
        var notificationId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Notifications.Add(CreateNotification("user-1", "Unread notification", notificationId));
            dbContext.SaveChanges();
        });
        using var client = factory.CreateClient();
        AddToken(client, "user-1");

        using var response = await client.PatchAsync($"/api/notifications/{notificationId}/read", null);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"success\":true", content);
        Assert.Contains("\"isRead\":true", content);
        Assert.Contains("+00:00", content);
    }

    [Fact]
    public async Task MarkAsRead_ForAnotherUserNotification_ReturnsNotFound()
    {
        var notificationId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Notifications.Add(CreateNotification("user-2", "Other notification", notificationId));
            dbContext.SaveChanges();
        });
        using var client = factory.CreateClient();
        AddToken(client, "user-1");

        using var response = await client.PatchAsync($"/api/notifications/{notificationId}/read", null);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Notification was not found.", content);
    }

    [Fact]
    public async Task MarkAsRead_WhenNotificationDoesNotExist_ReturnsNotFound()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        AddToken(client, "user-1");

        using var response = await client.PatchAsync($"/api/notifications/{Guid.NewGuid()}/read", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MarkAsRead_WithAdminTokenForAnotherUserNotification_ReturnsNotFound()
    {
        var notificationId = Guid.NewGuid();
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Notifications.Add(CreateNotification("user-2", "Other notification", notificationId));
            dbContext.SaveChanges();
        });
        using var client = factory.CreateClient();
        AddToken(client, "admin-user", AuthRoles.Admin);

        using var response = await client.PatchAsync($"/api/notifications/{notificationId}/read", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MarkAsRead_WhenAlreadyRead_PreservesOriginalReadAtUtc()
    {
        var notificationId = Guid.NewGuid();
        var originalReadAtUtc = DateTime.UtcNow.AddHours(-1);
        await using var factory = CreateFactory(dbContext =>
        {
            var notification = CreateNotification("user-1", "Already read notification", notificationId);
            notification.IsRead = true;
            notification.ReadAtUtc = originalReadAtUtc;
            dbContext.Notifications.Add(notification);
            dbContext.SaveChanges();
        });
        using var client = factory.CreateClient();
        AddToken(client, "user-1");

        using var firstResponse = await client.PatchAsync($"/api/notifications/{notificationId}/read", null);
        using var secondResponse = await client.PatchAsync($"/api/notifications/{notificationId}/read", null);
        using var document = JsonDocument.Parse(await secondResponse.Content.ReadAsStringAsync());
        var readAtUtc = document.RootElement
            .GetProperty("data")
            .GetProperty("notification")
            .GetProperty("readAtUtc")
            .GetDateTimeOffset();

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal(new DateTimeOffset(originalReadAtUtc, TimeSpan.Zero), readAtUtc);
    }

    [Fact]
    public async Task MarkAllAsRead_WithoutToken_ReturnsUnauthorized()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.PatchAsync("/api/notifications/read-all", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MarkAllAsRead_MarksOnlyCurrentUsersUnreadNotifications()
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Notifications.AddRange(
                CreateNotification("user-1", "Own unread one"),
                CreateNotification("user-1", "Own unread two"),
                CreateNotification("user-2", "Other unread"));
            dbContext.SaveChanges();
        });
        using var client = factory.CreateClient();
        AddToken(client, "user-1");

        using var response = await client.PatchAsync("/api/notifications/read-all", null);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"updatedCount\":2", content);

        AddToken(client, "user-2");
        using var otherNotificationsResponse = await client.GetAsync("/api/notifications");
        var otherNotifications = await otherNotificationsResponse.Content.ReadAsStringAsync();
        Assert.Contains("\"isRead\":false", otherNotifications);
    }

    [Fact]
    public async Task MarkAllAsRead_WithAdminToken_DoesNotMarkOtherUsersNotifications()
    {
        await using var factory = CreateFactory(dbContext =>
        {
            dbContext.Notifications.AddRange(
                CreateNotification("admin-user", "Admin unread"),
                CreateNotification("user-2", "Other unread"));
            dbContext.SaveChanges();
        });
        using var client = factory.CreateClient();
        AddToken(client, "admin-user", AuthRoles.Admin);

        using var response = await client.PatchAsync("/api/notifications/read-all", null);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"updatedCount\":1", content);

        AddToken(client, "user-2");
        using var otherNotificationsResponse = await client.GetAsync("/api/notifications");
        var otherNotifications = await otherNotificationsResponse.Content.ReadAsStringAsync();
        Assert.Contains("\"isRead\":false", otherNotifications);
    }

    [Fact]
    public async Task MarkAllAsRead_WhenNoUnreadNotifications_ReturnsSuccessWithZeroCount()
    {
        await using var factory = CreateFactory(dbContext =>
        {
            var notification = CreateNotification("user-1", "Already read");
            notification.IsRead = true;
            notification.ReadAtUtc = DateTime.UtcNow.AddMinutes(-1);
            dbContext.Notifications.Add(notification);
            dbContext.SaveChanges();
        });
        using var client = factory.CreateClient();
        AddToken(client, "user-1");

        using var response = await client.PatchAsync("/api/notifications/read-all", null);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"updatedCount\":0", content);
    }

    private static WebApplicationFactory<Program> CreateFactory(
        Action<ApplicationDbContext>? seed = null,
        string environment = "Production")
    {
        var databaseName = Guid.NewGuid().ToString();

        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(environment);
                builder.ConfigureAppConfiguration((_, configurationBuilder) =>
                {
                    configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["SeedDemoUsers"] = "false",
                        ["JwtSettings:Issuer"] = Issuer,
                        ["JwtSettings:Audience"] = Audience,
                        ["JwtSettings:SecretKey"] = SecretKey,
                        ["JwtSettings:ExpirationMinutes"] = "60"
                    });
                });
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<ApplicationDbContext>>();

                    services.AddDbContext<ApplicationDbContext>(options =>
                        options.UseInMemoryDatabase(databaseName));

                    if (seed is not null)
                    {
                        using var serviceProvider = services.BuildServiceProvider();
                        using var scope = serviceProvider.CreateScope();
                        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        seed(dbContext);
                    }
                });
            });
    }

    private static void AssertResponseCodes(JsonElement operation, params string[] expectedCodes)
    {
        var responses = operation.GetProperty("responses");

        foreach (var expectedCode in expectedCodes)
        {
            Assert.True(responses.TryGetProperty(expectedCode, out _), $"Expected response code {expectedCode}.");
        }
    }

    private static void AssertSchemaProperties(JsonElement schema, params string[] expectedProperties)
    {
        var properties = schema.GetProperty("properties");

        foreach (var expectedProperty in expectedProperties)
        {
            Assert.True(properties.TryGetProperty(expectedProperty, out _), $"Expected property {expectedProperty}.");
        }
    }

    private static Notification CreateNotification(string userId, string title, Guid? id = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            UserId = userId,
            Title = title,
            Message = $"{title} message",
            Type = "Test",
            CreatedAtUtc = DateTime.UtcNow
        };

    private static void AddToken(HttpClient client, string userId, string role = AuthRoles.Customer) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(userId, role));

    private static string CreateToken(string userId, string role)
    {
        var now = DateTime.UtcNow;
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(JwtRegisteredClaimNames.Email, "user@example.com"),
            new Claim(ClaimTypes.Email, "user@example.com"),
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, "Notification User"),
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
}

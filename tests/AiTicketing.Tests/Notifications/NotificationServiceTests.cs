using AiTicketing.Application.Common.Interfaces;
using AiTicketing.Application.Notifications;
using AiTicketing.Infrastructure.Notifications;
using AiTicketing.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace AiTicketing.Tests.Notifications;

public sealed class NotificationServiceTests
{
    [Fact]
    public async Task CreateAsync_StoresNotification()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var response = await service.CreateAsync(new CreateNotificationRequest(
            "user-1",
            "Ticket updated",
            "A ticket was updated.",
            "TicketStatusChanged",
            Guid.NewGuid()));

        var savedNotification = await dbContext.Notifications.SingleAsync();

        Assert.Equal(savedNotification.Id, response.Id);
        Assert.Equal("user-1", savedNotification.UserId);
        Assert.False(savedNotification.IsRead);
        Assert.Equal(TimeSpan.Zero, response.CreatedAtUtc.Offset);
    }

    [Fact]
    public async Task CreateAsync_SendsDocumentedEventToIntendedUserOnlyWithStablePayload()
    {
        await using var dbContext = CreateDbContext();
        var hubContext = new RecordingHubContext();
        var service = CreateService(dbContext, hubContext: hubContext);
        var ticketId = Guid.NewGuid();

        var response = await service.CreateAsync(new CreateNotificationRequest(
            "  recipient-user  ",
            "Ticket assigned",
            "A ticket was assigned.",
            "TicketAssigned",
            ticketId));

        Assert.Equal(["recipient-user"], hubContext.ClientsRecorder.SelectedUserIds);
        Assert.False(hubContext.ClientsRecorder.BroadcastWasRequested);
        Assert.Equal(NotificationHubContract.NotificationReceivedEvent, hubContext.ClientProxy.Method);
        var payload = Assert.IsType<NotificationResponse>(Assert.Single(hubContext.ClientProxy.Arguments));
        Assert.Equal(response, payload);

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.True(root.TryGetProperty("id", out _));
        Assert.True(root.TryGetProperty("title", out _));
        Assert.True(root.TryGetProperty("message", out _));
        Assert.True(root.TryGetProperty("type", out _));
        Assert.True(root.TryGetProperty("ticketId", out _));
        Assert.True(root.TryGetProperty("isRead", out _));
        Assert.True(root.TryGetProperty("createdAtUtc", out _));
        Assert.True(root.TryGetProperty("readAtUtc", out _));
    }

    [Fact]
    public async Task CreateAsync_WhenSignalRPushFails_StillStoresAndReturnsNotification()
    {
        await using var dbContext = CreateDbContext();
        var hubContext = new RecordingHubContext(throwOnSend: true);
        var service = CreateService(dbContext, hubContext: hubContext);

        var response = await service.CreateAsync(new CreateNotificationRequest(
            "recipient-user",
            "Ticket updated",
            "A ticket was updated.",
            "TicketStatusChanged",
            Guid.NewGuid()));

        var savedNotification = await dbContext.Notifications.SingleAsync();
        Assert.Equal(savedNotification.Id, response.Id);
        Assert.Equal("recipient-user", savedNotification.UserId);
        Assert.Equal(NotificationHubContract.NotificationReceivedEvent, hubContext.ClientProxy.Method);
    }

    [Fact]
    public async Task GetMyNotificationsAsync_ReturnsOnlyCurrentUserNotifications()
    {
        await using var dbContext = CreateDbContext();
        var ownNotification = CreateNotification("user-1", "Own notification", DateTime.UtcNow);
        var otherNotification = CreateNotification("user-2", "Other notification", DateTime.UtcNow.AddMinutes(1));
        dbContext.Notifications.AddRange(ownNotification, otherNotification);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "user-1"));

        var result = await service.GetMyNotificationsAsync();

        Assert.Single(result);
        Assert.Equal("Own notification", result[0].Title);
    }

    [Fact]
    public async Task GetMyNotificationsAsync_WhenUnauthenticated_ReturnsEmptyList()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Notifications.Add(CreateNotification("user-1", "Hidden notification", DateTime.UtcNow));
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var result = await service.GetMyNotificationsAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetUnreadCountAsync_ReturnsOnlyCurrentUsersUnreadNotificationCount()
    {
        await using var dbContext = CreateDbContext();
        var ownRead = CreateNotification("user-1", "Own read", DateTime.UtcNow);
        ownRead.IsRead = true;
        ownRead.ReadAtUtc = DateTime.UtcNow;
        dbContext.Notifications.AddRange(
            CreateNotification("user-1", "Own unread one", DateTime.UtcNow),
            CreateNotification("user-1", "Own unread two", DateTime.UtcNow),
            ownRead,
            CreateNotification("user-2", "Other unread", DateTime.UtcNow));
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "user-1"));

        var response = await service.GetUnreadCountAsync();

        Assert.Equal(2, response.UnreadCount);
    }

    [Fact]
    public async Task GetUnreadCountAsync_WhenUserHasNoUnreadNotifications_ReturnsZero()
    {
        await using var dbContext = CreateDbContext();
        var notification = CreateNotification("user-1", "Own read", DateTime.UtcNow);
        notification.IsRead = true;
        notification.ReadAtUtc = DateTime.UtcNow;
        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "user-1"));

        var response = await service.GetUnreadCountAsync();

        Assert.Equal(0, response.UnreadCount);
    }

    [Fact]
    public async Task GetUnreadCountAsync_WhenUnauthenticated_ReturnsZero()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Notifications.Add(CreateNotification("user-1", "Unread", DateTime.UtcNow));
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var response = await service.GetUnreadCountAsync();

        Assert.Equal(0, response.UnreadCount);
    }

    [Fact]
    public async Task MarkAsReadAsync_WhenNotificationBelongsToCurrentUser_MarksAsRead()
    {
        await using var dbContext = CreateDbContext();
        var notification = CreateNotification("user-1", "Unread notification", DateTime.UtcNow);
        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "user-1"));

        var response = await service.MarkAsReadAsync(notification.Id);
        var savedNotification = await dbContext.Notifications.SingleAsync();

        Assert.NotNull(response);
        Assert.True(response.Notification.IsRead);
        Assert.True(savedNotification.IsRead);
        Assert.NotNull(savedNotification.ReadAtUtc);
        Assert.Equal(TimeSpan.Zero, response.Notification.ReadAtUtc?.Offset);
    }

    [Fact]
    public async Task MarkAsReadAsync_WhenNotificationBelongsToAnotherUser_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();
        var notification = CreateNotification("user-2", "Other notification", DateTime.UtcNow);
        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "user-1"));

        var response = await service.MarkAsReadAsync(notification.Id);

        Assert.Null(response);
    }

    [Fact]
    public async Task MarkAsReadAsync_WhenNotificationIsAlreadyRead_PreservesOriginalReadAtUtc()
    {
        await using var dbContext = CreateDbContext();
        var originalReadAtUtc = DateTime.UtcNow.AddHours(-1);
        var notification = CreateNotification("user-1", "Already read notification", DateTime.UtcNow.AddHours(-2));
        notification.IsRead = true;
        notification.ReadAtUtc = originalReadAtUtc;
        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "user-1"));

        var response = await service.MarkAsReadAsync(notification.Id);
        var savedNotification = await dbContext.Notifications.SingleAsync();

        Assert.NotNull(response);
        Assert.True(response.Notification.IsRead);
        Assert.Equal(originalReadAtUtc, savedNotification.ReadAtUtc);
        Assert.Equal(new DateTimeOffset(originalReadAtUtc, TimeSpan.Zero), response.Notification.ReadAtUtc);
    }

    [Fact]
    public async Task MarkAsReadAsync_WhenNotificationDoesNotExist_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "user-1"));

        var response = await service.MarkAsReadAsync(Guid.NewGuid());

        Assert.Null(response);
    }

    [Fact]
    public async Task MarkAsReadAsync_WhenUnauthenticated_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();
        var notification = CreateNotification("user-1", "Unread notification", DateTime.UtcNow);
        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var response = await service.MarkAsReadAsync(notification.Id);

        Assert.Null(response);
    }

    [Fact]
    public async Task MarkAllAsReadAsync_MarksOnlyCurrentUsersUnreadNotifications()
    {
        await using var dbContext = CreateDbContext();
        var ownUnreadOne = CreateNotification("user-1", "Own unread one", DateTime.UtcNow.AddMinutes(-3));
        var ownUnreadTwo = CreateNotification("user-1", "Own unread two", DateTime.UtcNow.AddMinutes(-2));
        var otherUnread = CreateNotification("user-2", "Other unread", DateTime.UtcNow.AddMinutes(-1));
        dbContext.Notifications.AddRange(ownUnreadOne, ownUnreadTwo, otherUnread);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "user-1"));

        var response = await service.MarkAllAsReadAsync();
        var notifications = await dbContext.Notifications.ToListAsync();

        Assert.Equal(2, response.UpdatedCount);
        Assert.All(notifications.Where(notification => notification.UserId == "user-1"), notification =>
        {
            Assert.True(notification.IsRead);
            Assert.NotNull(notification.ReadAtUtc);
        });
        Assert.False(otherUnread.IsRead);
        Assert.Null(otherUnread.ReadAtUtc);
    }

    [Fact]
    public async Task MarkAllAsReadAsync_PreservesAlreadyReadNotificationTimestamp()
    {
        await using var dbContext = CreateDbContext();
        var originalReadAtUtc = DateTime.UtcNow.AddHours(-2);
        var alreadyRead = CreateNotification("user-1", "Already read", DateTime.UtcNow.AddHours(-3));
        alreadyRead.IsRead = true;
        alreadyRead.ReadAtUtc = originalReadAtUtc;
        var unread = CreateNotification("user-1", "Unread", DateTime.UtcNow);
        dbContext.Notifications.AddRange(alreadyRead, unread);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "user-1"));

        var response = await service.MarkAllAsReadAsync();

        Assert.Equal(1, response.UpdatedCount);
        Assert.Equal(originalReadAtUtc, alreadyRead.ReadAtUtc);
        Assert.True(unread.IsRead);
        Assert.NotNull(unread.ReadAtUtc);
    }

    [Fact]
    public async Task MarkAllAsReadAsync_WhenThereAreNoUnreadNotifications_ReturnsZero()
    {
        await using var dbContext = CreateDbContext();
        var notification = CreateNotification("user-1", "Already read", DateTime.UtcNow);
        notification.IsRead = true;
        notification.ReadAtUtc = DateTime.UtcNow.AddMinutes(-1);
        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, new StubCurrentUserService(
            isAuthenticated: true,
            userId: "user-1"));

        var response = await service.MarkAllAsReadAsync();

        Assert.Equal(0, response.UpdatedCount);
    }

    [Fact]
    public async Task MarkAllAsReadAsync_WhenUnauthenticated_ReturnsZeroAndChangesNothing()
    {
        await using var dbContext = CreateDbContext();
        var notification = CreateNotification("user-1", "Unread", DateTime.UtcNow);
        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var response = await service.MarkAllAsReadAsync();

        Assert.Equal(0, response.UpdatedCount);
        Assert.False(notification.IsRead);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static NotificationService CreateService(
        ApplicationDbContext dbContext,
        ICurrentUserService? currentUserService = null,
        IHubContext<NotificationHub>? hubContext = null) =>
        new(
            dbContext,
            currentUserService ?? new StubCurrentUserService(),
            hubContext ?? new NoOpHubContext(),
            NullLogger<NotificationService>.Instance);

    private static Domain.Entities.Notification CreateNotification(
        string userId,
        string title,
        DateTime createdAtUtc) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title,
            Message = $"{title} message",
            Type = "Test",
            CreatedAtUtc = createdAtUtc
        };

    private sealed class StubCurrentUserService(
        bool isAuthenticated = false,
        string? userId = null) : ICurrentUserService
    {
        public bool IsAuthenticated { get; } = isAuthenticated;

        public string? UserId { get; } = userId;

        public string? Email => null;

        public string? FullName => null;

        public string? Role => null;
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

    private sealed class RecordingHubContext(bool throwOnSend = false) : IHubContext<NotificationHub>
    {
        public RecordingHubClients ClientsRecorder => (RecordingHubClients)Clients;

        public RecordingClientProxy ClientProxy => ClientsRecorder.ClientProxy;

        public IHubClients Clients { get; } = new RecordingHubClients(new RecordingClientProxy(throwOnSend));

        public IGroupManager Groups { get; } = new NoOpGroupManager();
    }

    private sealed class RecordingHubClients : IHubClients
    {
        private readonly RecordingClientProxy clientProxy;

        public RecordingHubClients(RecordingClientProxy clientProxy)
        {
            this.clientProxy = clientProxy;
        }

        public RecordingClientProxy ClientProxy => clientProxy;

        public List<string> SelectedUserIds { get; } = [];

        public bool BroadcastWasRequested { get; private set; }

        public IClientProxy All
        {
            get
            {
                BroadcastWasRequested = true;
                return clientProxy;
            }
        }

        public IClientProxy User(string userId)
        {
            SelectedUserIds.Add(userId);
            return clientProxy;
        }

        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => clientProxy;

        public IClientProxy Client(string connectionId) => clientProxy;

        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => clientProxy;

        public IClientProxy Group(string groupName) => clientProxy;

        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => clientProxy;

        public IClientProxy Groups(IReadOnlyList<string> groupNames) => clientProxy;

        public IClientProxy Users(IReadOnlyList<string> userIds) => clientProxy;
    }

    private sealed class RecordingClientProxy(bool throwOnSend) : IClientProxy
    {
        public string? Method { get; private set; }

        public object?[] Arguments { get; private set; } = [];

        public Task SendCoreAsync(
            string method,
            object?[] args,
            CancellationToken cancellationToken = default)
        {
            Method = method;
            Arguments = args;

            return throwOnSend
                ? Task.FromException(new InvalidOperationException("SignalR push failed."))
                : Task.CompletedTask;
        }
    }
}

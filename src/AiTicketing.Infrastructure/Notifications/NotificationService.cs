using AiTicketing.Application.Common.Interfaces;
using AiTicketing.Application.Notifications;
using AiTicketing.Domain.Entities;
using AiTicketing.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AiTicketing.Infrastructure.Notifications;

public sealed class NotificationService(
    ApplicationDbContext dbContext,
    ICurrentUserService currentUserService,
    IHubContext<NotificationHub> hubContext,
    ILogger<NotificationService> logger) : INotificationService
{
    public async Task<NotificationResponse> CreateAsync(
        CreateNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId.Trim(),
            Title = request.Title.Trim(),
            Message = request.Message.Trim(),
            Type = request.Type.Trim(),
            TicketId = request.TicketId,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync(cancellationToken);

        var dto = MapToDto(notification);

        try
        {
            await hubContext.Clients
                .User(notification.UserId)
                .SendAsync(NotificationHubContract.NotificationReceivedEvent, dto, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to push notification {NotificationId} to user {UserId}.", notification.Id, request.UserId);
        }

        return dto;
    }

    public async Task<IReadOnlyList<NotificationResponse>> GetMyNotificationsAsync(CancellationToken cancellationToken = default)
    {
        if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
        {
            return [];
        }

        var userId = currentUserService.UserId;

        var notifications = await dbContext.Notifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId)
            .OrderByDescending(notification => notification.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return notifications.Select(MapToDto).ToArray();
    }

    public async Task<MarkNotificationAsReadResponse?> MarkAsReadAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
        {
            return null;
        }

        var userId = currentUserService.UserId;
        var notification = await dbContext.Notifications
            .SingleOrDefaultAsync(
                notification => notification.Id == notificationId && notification.UserId == userId,
                cancellationToken);

        if (notification is null)
        {
            return null;
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new MarkNotificationAsReadResponse(MapToDto(notification));
    }

    public async Task<MarkAllNotificationsAsReadResponse> MarkAllAsReadAsync(
        CancellationToken cancellationToken = default)
    {
        if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
        {
            return new MarkAllNotificationsAsReadResponse(0);
        }

        var userId = currentUserService.UserId;
        var unreadNotifications = await dbContext.Notifications
            .Where(notification => notification.UserId == userId && !notification.IsRead)
            .ToListAsync(cancellationToken);

        if (unreadNotifications.Count == 0)
        {
            return new MarkAllNotificationsAsReadResponse(0);
        }

        var readAtUtc = DateTime.UtcNow;
        foreach (var notification in unreadNotifications)
        {
            notification.IsRead = true;
            notification.ReadAtUtc = readAtUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new MarkAllNotificationsAsReadResponse(unreadNotifications.Count);
    }

    public async Task<UnreadNotificationCountResponse> GetUnreadCountAsync(
        CancellationToken cancellationToken = default)
    {
        if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
        {
            return new UnreadNotificationCountResponse(0);
        }

        var userId = currentUserService.UserId;
        var unreadCount = await dbContext.Notifications
            .AsNoTracking()
            .CountAsync(
                notification => notification.UserId == userId && !notification.IsRead,
                cancellationToken);

        return new UnreadNotificationCountResponse(unreadCount);
    }

    private static NotificationResponse MapToDto(Notification notification) =>
        new(
            notification.Id,
            notification.UserId,
            notification.Title,
            notification.Message,
            notification.Type,
            notification.TicketId,
            notification.IsRead,
            ToUtcOffset(notification.CreatedAtUtc),
            ToUtcOffset(notification.ReadAtUtc));

    private static DateTimeOffset ToUtcOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc), TimeSpan.Zero);

    private static DateTimeOffset? ToUtcOffset(DateTime? value) =>
        value is null ? null : ToUtcOffset(value.Value);
}

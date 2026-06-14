namespace AiTicketing.Application.Notifications;

public interface INotificationService
{
    Task<NotificationResponse> CreateAsync(CreateNotificationRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationResponse>> GetMyNotificationsAsync(CancellationToken cancellationToken = default);

    Task<MarkNotificationAsReadResponse?> MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default);

    Task<MarkAllNotificationsAsReadResponse> MarkAllAsReadAsync(CancellationToken cancellationToken = default);

    Task<UnreadNotificationCountResponse> GetUnreadCountAsync(CancellationToken cancellationToken = default);
}

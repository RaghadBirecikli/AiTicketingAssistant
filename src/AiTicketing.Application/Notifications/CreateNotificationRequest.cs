namespace AiTicketing.Application.Notifications;

public sealed record CreateNotificationRequest(
    string UserId,
    string Title,
    string Message,
    string Type,
    Guid? TicketId);

namespace AiTicketing.Application.Notifications;

public sealed record NotificationResponse(
    Guid Id,
    string UserId,
    string Title,
    string Message,
    string Type,
    Guid? TicketId,
    bool IsRead,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ReadAtUtc);

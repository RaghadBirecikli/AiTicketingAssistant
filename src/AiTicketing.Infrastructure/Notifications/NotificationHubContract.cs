namespace AiTicketing.Infrastructure.Notifications;

public static class NotificationHubContract
{
    public const string Route = "/hubs/notifications";

    public const string NotificationReceivedEvent = "NotificationReceived";
}

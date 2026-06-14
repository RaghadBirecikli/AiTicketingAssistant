using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AiTicketing.Infrastructure.Notifications;

[Authorize]
public sealed class NotificationHub : Hub
{
}

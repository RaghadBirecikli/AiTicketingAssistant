using AiTicketing.Application.Common.Models;
using AiTicketing.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiTicketing.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
[Produces("application/json")]
public sealed class NotificationsController(INotificationService notificationService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<NotificationResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<NotificationResponse>>>> GetMyNotifications(
        CancellationToken cancellationToken)
    {
        var notifications = await notificationService.GetMyNotificationsAsync(cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<NotificationResponse>>.Ok(notifications));
    }

    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(ApiResponse<UnreadNotificationCountResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<UnreadNotificationCountResponse>>> GetUnreadCount(
        CancellationToken cancellationToken)
    {
        var response = await notificationService.GetUnreadCountAsync(cancellationToken);

        return Ok(ApiResponse<UnreadNotificationCountResponse>.Ok(response));
    }

    [HttpPatch("{id:guid}/read")]
    [ProducesResponseType(typeof(ApiResponse<MarkNotificationAsReadResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<MarkNotificationAsReadResponse>>> MarkAsRead(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await notificationService.MarkAsReadAsync(id, cancellationToken);

        if (response is null)
        {
            return NotFound(ApiResponse<object>.Fail("Notification was not found."));
        }

        return Ok(ApiResponse<MarkNotificationAsReadResponse>.Ok(response, "Notification marked as read."));
    }

    [HttpPatch("read-all")]
    [ProducesResponseType(typeof(ApiResponse<MarkAllNotificationsAsReadResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<MarkAllNotificationsAsReadResponse>>> MarkAllAsRead(
        CancellationToken cancellationToken)
    {
        var response = await notificationService.MarkAllAsReadAsync(cancellationToken);

        return Ok(ApiResponse<MarkAllNotificationsAsReadResponse>.Ok(response, "Notifications marked as read."));
    }
}

# SignalR Notifications Contract

## Connection

- Hub route: `/hubs/notifications`
- Authentication: required
- Browser SignalR clients may provide the JWT through the `access_token` query string.
- The authenticated SignalR user id is resolved from the JWT `NameIdentifier` claim, with `sub` as a fallback.

## Live Notification Event

- Event name: `NotificationReceived`
- Delivery: sent only to the notification recipient through `Clients.User(userId)`

Payload:

```json
{
  "id": "notification-id",
  "userId": "recipient-user-id",
  "title": "Ticket assigned",
  "message": "Ticket 'Payment issue' has been assigned to you.",
  "type": "TicketAssigned",
  "ticketId": "ticket-id",
  "isRead": false,
  "createdAtUtc": "2026-06-04T12:00:00+00:00",
  "readAtUtc": null
}
```

The payload uses the same `NotificationResponse` contract as the Notifications REST API.
Database persistence completes before live delivery is attempted. A SignalR delivery failure is logged and does not undo or fail notification creation.

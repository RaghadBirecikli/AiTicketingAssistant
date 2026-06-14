# Frontend API Contract

This document summarizes the backend contracts used by the frontend. JSON property names are camelCase. Enum values are serialized as strings. UTC timestamps include a `Z` or `+00:00` offset.

## Common Conventions

Protected REST endpoints use a JWT bearer token:

```http
Authorization: Bearer <token>
```

Successful REST responses use this envelope:

```json
{
  "success": true,
  "data": {},
  "message": null,
  "errors": null
}
```

### Status Codes

| Status | Meaning |
|---|---|
| `400` | Invalid request body or query parameter |
| `401` | Authentication is required or the authenticated identity is no longer valid |
| `403` | The authenticated user does not have the required role |
| `404` | Resource is missing, deleted, or inaccessible under ownership rules |
| `429` | AI endpoint rate limit exceeded; handle `Retry-After` when present |
| `503` | Selected AI provider is unavailable and configured fallback is disabled |

## Authentication And Current User

### `GET /api/me`

Returns the authenticated user's public profile and current Identity roles.

- Authentication: required
- Roles: Admin, Agent, or Customer

```json
{
  "id": "user-id",
  "email": "user@example.com",
  "displayName": "User Name",
  "roles": ["Agent"]
}
```

## Tickets

### `GET /api/tickets`

Returns a filtered and paginated list of visible, non-deleted tickets.

- Authentication: required
- Admin sees all non-deleted tickets.
- Agent sees only tickets assigned to their user ID.
- Customer sees only tickets owned by their user ID.

| Query parameter | Notes |
|---|---|
| `status` | Optional: `Open`, `InProgress`, `WaitingForCustomer`, `Resolved`, `Closed` |
| `priority` | Optional: `Low`, `Medium`, `High`, `Urgent` |
| `assignedToUserId` | Admin may filter by any assignee; Agent may provide only their own ID; Customer cannot use it |
| `unassigned` | Admin-only when `true`; conflicts with `assignedToUserId` |
| `search` | Searches title and description; trimmed; maximum 100 characters |
| `page` | Default `1`; minimum `1` |
| `pageSize` | Default `20`; range `1` to `100` |
| `sortBy` | Optional: `createdAt`, `updatedAt`, `priority`, `status` |
| `sortDirection` | Optional: `asc` or `desc`; defaults to `desc` when sorting |

Filters combine using AND. Invalid or role-incompatible query parameters return `400`.

```json
{
  "items": [
    {
      "id": "ticket-id",
      "title": "Payment page is not working",
      "description": "Payment fails during checkout.",
      "status": "Open",
      "priority": "High",
      "category": "Billing",
      "source": "Web",
      "customerEmail": "customer@example.com",
      "customerName": "Sara Ahmed",
      "customerUserId": "customer-user-id",
      "assignedToUserId": "agent-user-id",
      "createdAtUtc": "2026-06-04T12:00:00+00:00",
      "updatedAtUtc": null,
      "resolvedAtUtc": null,
      "closedAtUtc": null
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1,
  "hasPreviousPage": false,
  "hasNextPage": false
}
```

### `GET /api/tickets/{id}`

Returns ticket details and conversation messages.

- Authentication: required
- Admin can view any non-deleted ticket.
- Agent can view assigned tickets.
- Customer can view owned tickets.
- Missing, deleted, or inaccessible tickets return `404`.

```json
{
  "id": "ticket-id",
  "title": "Payment page is not working",
  "description": "Payment fails during checkout.",
  "status": "InProgress",
  "priority": "High",
  "category": "Billing",
  "source": "Web",
  "customerEmail": "customer@example.com",
  "customerName": "Sara Ahmed",
  "customerUserId": "customer-user-id",
  "assignedToUserId": "agent-user-id",
  "createdAtUtc": "2026-06-04T12:00:00+00:00",
  "updatedAtUtc": "2026-06-04T12:15:00+00:00",
  "resolvedAtUtc": null,
  "closedAtUtc": null,
  "messages": [
    {
      "id": "message-id",
      "ticketId": "ticket-id",
      "senderUserId": "agent-user-id",
      "senderRole": "Agent",
      "senderDisplayName": "Support Agent",
      "body": "We are investigating the payment error.",
      "isInternalNote": false,
      "createdAtUtc": "2026-06-04T12:15:00+00:00"
    }
  ]
}
```

Messages are ordered by `createdAtUtc` ascending.

## Ticket Workflows

| Method and route | Authorization | Request body |
|---|---|---|
| `POST /api/tickets` | Public; authenticated Customers become ticket owners | `{ "title", "description", "customerEmail?", "customerName?", "source" }` |
| `PATCH /api/tickets/{id}/assign` | Admin only | `{ "assignedToUserId", "assignedToDisplayName?", "assignedByUserId?", "assignedByDisplayName?" }` |
| `PATCH /api/tickets/{id}/status` | Admin, or assigned Agent | `{ "status", "changedByUserId?", "changedByDisplayName?" }` |
| `POST /api/tickets/{id}/messages` | Admin; assigned Agent; owning Customer | `{ "message", "isInternalNote", "createdByUserId?", "createdByDisplayName?" }` |
| `POST /api/tickets/{id}/internal-notes` | Admin, or assigned Agent | `{ "body" }`; maximum 4000 characters |
| `POST /api/tickets/{id}/ai/suggest-reply` | Admin, or assigned Agent | Optional `{ "instruction" }`; returns `{ "suggestedReply" }` without sending it |
| `POST /api/tickets/{id}/ai/summarize` | Admin, or assigned Agent | Optional `{ "includeInternalNotes": false }`; returns `{ "summary" }` without persisting it |
| `POST /api/tickets/{id}/ai/suggest-triage` | Admin, or assigned Agent | Optional `{ "instruction" }`; returns advisory triage fields without applying them |

Customers cannot add internal notes. Authenticated user identity overrides request-provided actor fields where applicable.

AI summaries are read-only. They do not create messages, notifications, SignalR events, audit logs, or ticket state changes. By default, summaries exclude internal notes. Admins may explicitly set `includeInternalNotes` to `true`; assigned Agents receive `403` if they request internal notes. If the selected AI provider is unavailable and fallback is disabled, the endpoint can return `503`.

AI triage suggestions are also read-only. They never update ticket priority, category, status, assignment, ownership, title, description, or messages. The optional `instruction` is trimmed, whitespace becomes `null`, and the maximum length is 500 characters. Internal notes are always excluded. Allowed priority values are `Low`, `Medium`, `High`, and `Urgent`. Category suggestions are domain-backed by the existing `TicketCategory` enum: `General`, `Bug`, `FeatureRequest`, `Billing`, `TechnicalSupport`, and `Complaint`; they are advisory only and are not persisted. Agent recommendation is intentionally not included yet because the backend only has a basic active Agent lookup and no workload, capability, queue, or availability data for meaningful assignment recommendations. If the selected AI provider is unavailable and fallback is disabled, the endpoint can return `503`.

All AI ticket endpoints are rate limited with a shared authenticated-user quota. This includes suggested replies, summaries, and triage suggestions. A `429` response uses the standard API wrapper and may include `Retry-After`. Frontends should disable repeated submit actions while a request is pending, avoid auto-triggering advisory AI operations in loops, and retry only after the server-provided delay when present. Success request and response shapes are unchanged.

Triage response:

```json
{
  "currentPriority": "Medium",
  "suggestedPriority": "High",
  "suggestedCategory": "Billing",
  "escalationRecommended": true,
  "escalationReason": "The customer reports repeated payment failures.",
  "rationale": "The issue blocks checkout and has repeated failure context."
}
```

## Ticket Dashboard Stats

### `GET /api/tickets/stats`

Admin-only summary of all non-deleted tickets:

```json
{
  "total": 42,
  "open": 10,
  "inProgress": 15,
  "resolved": 12,
  "closed": 5,
  "unassigned": 8,
  "lowPriority": 4,
  "mediumPriority": 20,
  "highPriority": 12,
  "urgentPriority": 6
}
```

### `GET /api/tickets/my-stats`

Agent/Customer-only summary. Agent counts are scoped to assigned tickets; Customer counts are scoped to owned tickets.

```json
{
  "total": 12,
  "open": 3,
  "inProgress": 4,
  "resolved": 3,
  "closed": 2,
  "lowPriority": 1,
  "mediumPriority": 5,
  "highPriority": 4,
  "urgentPriority": 2
}
```

## Users

### `GET /api/users/agents`

Admin-only lookup of active Agent-role users, ordered by display name and then email.

```json
[
  {
    "id": "agent-user-id",
    "email": "agent@example.com",
    "displayName": "Support Agent"
  }
]
```

## Notifications

All notification REST endpoints require authentication and operate only on the current user's notifications, including for Admin users.

### `GET /api/notifications`

Returns `NotificationResponse[]`, newest first:

```json
[
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
]
```

### `GET /api/notifications/unread-count`

```json
{
  "unreadCount": 3
}
```

### `PATCH /api/notifications/{id}/read`

Marks one owned notification as read. Returns:

```json
{
  "notification": {
    "id": "notification-id",
    "userId": "recipient-user-id",
    "title": "Ticket assigned",
    "message": "Ticket 'Payment issue' has been assigned to you.",
    "type": "TicketAssigned",
    "ticketId": "ticket-id",
    "isRead": true,
    "createdAtUtc": "2026-06-04T12:00:00+00:00",
    "readAtUtc": "2026-06-04T12:10:00+00:00"
  }
}
```

### `PATCH /api/notifications/read-all`

Marks all unread notifications owned by the current user as read.

```json
{
  "updatedCount": 3
}
```

## SignalR Notifications

- Hub route: `/hubs/notifications`
- Authentication: required
- Event name: `NotificationReceived`
- Event payload: `NotificationResponse`
- JS/TS package: `@microsoft/signalr`

Use the JWT with `accessTokenFactory`:

```ts
import * as signalR from "@microsoft/signalr";

const connection = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/notifications", {
    accessTokenFactory: () => token
  })
  .withAutomaticReconnect()
  .build();

connection.on("NotificationReceived", notification => {
  // Update the notification list and unread count.
});
```

Live events are sent only to the notification recipient. Notification persistence succeeds even if live delivery fails.

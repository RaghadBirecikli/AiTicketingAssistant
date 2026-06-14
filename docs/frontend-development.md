# Frontend Development

The Angular frontend lives in `src/AiTicketing.Web`.

## Stack

| Area | Choice |
|---|---|
| Framework | Angular 19.1.6 |
| Language | TypeScript strict mode |
| Components | Standalone Angular components |
| Routing | Angular Router |
| HTTP | Angular HttpClient |
| Forms | Reactive Forms |
| Local auth state | Angular Signals |
| Tests | Angular CLI Karma/Jasmine |

## Design System

The frontend uses a lightweight custom SCSS design system rather than a large UI framework. Global tokens live in `src/AiTicketing.Web/src/styles.scss` as semantic CSS custom properties.

Token groups:

| Group | Examples |
|---|---|
| Color | `--bg`, `--surface`, `--text-primary`, `--border`, `--primary`, `--success`, `--warning`, `--danger`, `--info`, `--internal-note`, `--ai-accent` |
| Typography | `--font-sans` with system UI, Segoe UI, Tahoma, Arial, and generic sans-serif fallback for English/Arabic readiness |
| Spacing and shape | component-local spacing plus shared `--radius-xs`, `--radius-sm`, `--radius-md`, `--radius-lg` |
| Effects | `--shadow-sm`, `--shadow-md`, `--shadow-lg`, `--focus-ring` |
| Layout | `--container`, responsive component breakpoints, `--z-drawer`, `--z-popover`, `--z-toast` |
| Motion | `--transition-fast`, `--transition`; reduced-motion users receive near-zero animation duration |

Use semantic tokens instead of repeated hardcoded colors. New feature styles should prefer existing global primitives:

- `.button`, `.button-secondary`, `.button-outline`, `.button-ghost`, `.button-danger`
- `.form-field`, `.field-help`, `.field-error`
- `.badge`, `.pill`
- `.state-panel`, `.feedback`

The visual direction is a calm B2B SaaS helpdesk interface: compact scanning density, quiet surfaces, clear status colors, restrained shadows, and no decorative external imagery.

Icons use the shared standalone `UiIconComponent` in `src/AiTicketing.Web/src/app/shared/components/ui-icon`. It provides a small inline SVG set with one stroke style and grid, so feature components should not introduce emoji, ASCII glyphs, or unrelated icon libraries for product UI.

## Theme Behavior

The app supports `system`, `light`, and `dark` theme preferences through `ThemeService`.

| Behavior | Detail |
|---|---|
| Initial default | `system` |
| Persistence | `localStorage["ai-ticketing-theme"]` |
| Applied attributes | `html[data-theme-preference]` and `html[data-theme]` |
| No-flash setup | `src/index.html` includes a small head script that applies the stored/system theme before Angular bootstraps |
| Auth separation | Theme preference is stored separately from session/JWT data |

The authenticated shell exposes a segmented theme switcher. To preview themes locally, run `npm start`, sign in, then use the System/Light/Dark control in the header.

## Localization Behavior

The Angular frontend supports runtime English and Arabic without separate builds. Localization is owned by `LocalizationService` in `src/AiTicketing.Web/src/app/core/localization`.

| Behavior | Detail |
|---|---|
| Default language | English (`en`) |
| Supported languages | English (`en`) and Modern Standard Arabic (`ar`) |
| Persistence | `localStorage["ai-ticketing-language"]` |
| Applied attributes | `html[lang]`, `html[dir]`, `html[data-language]`, and `html[data-direction]` |
| No-flash setup | `src/index.html` applies stored language and direction before Angular bootstraps |
| Auth/theme separation | Language uses its own storage key and does not read or mutate auth/session/theme storage |

Language switching happens in the authenticated header and on the login page. The selector is compact, keyboard-accessible, and changes the active language immediately without reloading the app.

Translation keys are semantic and grouped by feature in `translations.ts`:

- `common`
- `language`
- `theme`
- `navigation`
- `auth`
- `dashboard`
- `tickets`
- `ticketDetails`
- `messages`
- `workflows`
- `ai`
- `notifications`
- `errors`
- `enums`

Do not translate user-generated or backend dynamic content in the frontend. Ticket titles, descriptions, messages, internal notes, customer/agent names, emails, IDs, AI-generated output, and notification title/message fields must render exactly as returned by the API. Use `dir="auto"` for mixed-direction dynamic text where practical, and keep emails/IDs left-to-right with `dir="ltr"`.

Known domain enum labels are localized only for display. API payload values remain unchanged, for example `InProgress`, `Urgent`, and `TechnicalSupport`.

Styles should continue to prefer CSS logical properties (`padding-inline`, `margin-inline`, `inset-inline`, `border-inline`) so Arabic RTL layout works without duplicating component styles. The authenticated sidebar appears on the right in Arabic, and the mobile drawer opens from the right. Mirror only directional icons such as chevrons; semantic icons such as status, ticket, user, and notification icons should not be mirrored.

Dates and numbers should use the active locale. The shared `localDateTime` pipe reacts to language changes and formats timestamps with `Intl.DateTimeFormat`.

## Layout Behavior

Authenticated pages use `AuthenticatedLayoutComponent`.

Reusable authenticated shell styles live in `src/AiTicketing.Web/src/styles/app-shell.scss` and are loaded from the global stylesheet. Keep broad shell layout rules there so the shell stays consistent and component-level styles remain small.

Desktop:

- Collapsible sidebar with role-aware navigation.
- Active route indication.
- Sticky top header with page title, theme switcher, notification bell, and safe user menu.
- Main content constrained by `--container`.
- Sidebar collapsed preference persists in `localStorage["ai-ticketing-sidebar-collapsed"]`.

Mobile:

- Compact top bar with menu button.
- Drawer-style navigation with scrim close target.
- Header actions stack without horizontal overflow.
- Notification panel becomes a bottom sheet.

The layout avoids exposing JWTs or internal user IDs. The user menu shows display name, email, role, and logout only.

## Major UI Components

| Area | Components |
|---|---|
| Shell | `AuthenticatedLayoutComponent`, `NotificationBellComponent`, `NotificationPanelComponent`, `NotificationItemComponent` |
| Dashboard | `TicketDashboardPageComponent`, `DashboardStatCardComponent`, `DashboardQuickActionComponent` |
| Tickets | `TicketListPageComponent`, `TicketDetailsPageComponent`, `TicketSummaryCardComponent`, `TicketConversationComponent`, `TicketMessageComponent` |
| Workflows | `TicketMessageComposerComponent`, `TicketAssignmentControlComponent`, `TicketStatusControlComponent`, `TicketCreateFormComponent` |
| AI | `TicketAiPanelComponent` with Reply, Summary, and Triage tabs |

Top-level routes use `loadComponent` for page-level lazy loading. Keep new feature pages lazy-loaded unless there is a clear reason to place them in the initial bundle.

## Accessibility Notes

The UI uses semantic sections, headings, labels, and form controls. Interactive elements have visible focus rings through `--focus-ring`. Notification and form feedback uses `role="status"` or `role="alert"` where appropriate. AI tabs use tab semantics, mobile navigation exposes `aria-expanded`, and notification/user controls avoid rendering sensitive internal data.

Messages and AI output are rendered as text, not HTML. Internal notes are explicitly labeled and visually distinct beyond color. Long titles/messages wrap safely. Reduced-motion preferences are respected globally. Styles use logical properties such as `margin-inline`, `padding-inline`, and `inset-inline` where practical so future RTL support is easier.

## Backend Contract

The frontend uses these backend endpoints:

| Purpose | Endpoint |
|---|---|
| Login | `POST /api/auth/login` |
| Current user | `GET /api/me` |

Responses are wrapped in the backend `ApiResponse<T>` envelope. JSON is camelCase.

## Local URLs

| App | URL |
|---|---|
| Angular dev server | `http://localhost:4200` |
| ASP.NET Core API | `https://localhost:7194` |

The backend has a Development-only CORS policy for `http://localhost:4200`. Production does not allow wildcard origins.

## Configure API Base URL

Development:

```ts
// src/environments/environment.ts
apiBaseUrl: 'https://localhost:7194'
```

Production:

```ts
// src/environments/environment.production.ts
apiBaseUrl: ''
```

Use an empty production URL when the frontend is served from the same origin as the API. Otherwise set the deployed API origin. Do not store secrets in frontend configuration.

## Commands

From `src/AiTicketing.Web`:

```bash
npm install
npm start
npm run typecheck
npm test
npm run build
```

`npm test` runs Chrome Headless once.

## Authentication Notes

The backend currently returns a JWT to browser JavaScript. This frontend stores it in `sessionStorage` through `AuthStorageService` only. Components do not read or write browser storage directly and the UI never renders tokens.

On startup, the app reads the stored token and calls `GET /api/me`. If the token is invalid or expired, the session is cleared. Role behavior is deterministic:

1. Admin
2. Agent
3. Customer

Home routing follows the primary role:

| Role | Route |
|---|---|
| Admin | `/admin` |
| Agent | `/agent` |
| Customer | `/customer` |

## Ticket List

The shared Angular ticket list page uses `GET /api/tickets` through the centralized API service. It never calls `HttpClient` directly from the page component.

| Role | Route | Visible filters |
|---|---|---|
| Admin | `/admin/tickets` | search, status, priority, assigned agent, unassigned, sorting, pagination |
| Agent | `/agent/tickets` | search, status, priority, sorting, pagination |
| Customer | `/customer/tickets` | search, status, priority, sorting, pagination |

Admin agent options are loaded from `GET /api/users/agents`. Agent and Customer users do not request that endpoint.

Ticket list state is kept in URL query parameters where practical:

- `search`
- `status`
- `priority`
- `assignedToUserId` for Admin only
- `unassigned` for Admin only
- `page`
- `pageSize`
- `sortBy`
- `sortDirection`

Search text is trimmed before being sent to the backend. Empty search is omitted. Applying filters or changing sorting resets to page 1. Changing page size also resets to page 1. Invalid or role-inappropriate URL query values are ignored by the frontend before calling the backend; backend authorization and validation remain authoritative.

Run locally by starting the ASP.NET Core API at `https://localhost:7194`, then from `src/AiTicketing.Web` run:

```bash
npm start
```

## Role Dashboards

The authenticated dashboard routes render ticket statistics through the dashboard data-access service. Page components do not call `HttpClient` directly.

| Role | Route | Endpoint |
|---|---|---|
| Admin | `/admin` | `GET /api/tickets/stats` |
| Agent | `/agent` | `GET /api/tickets/my-stats` |
| Customer | `/customer` | `GET /api/tickets/my-stats` |

Admin statistics render only fields returned by the backend: total, open, in-progress, resolved, closed, unassigned, and urgent. Agent and Customer dashboards render scoped totals, open, in-progress, resolved, closed, and urgent. The current backend stats DTOs do not include a `waitingForCustomer` count, so the frontend does not invent or display that metric.

Dashboard quick actions use existing ticket-list query parameters only:

- Admin: `/admin/tickets`, `/admin/tickets?status=Open`, `/admin/tickets?unassigned=true`, `/admin/tickets?priority=Urgent`
- Agent: `/agent/tickets`, `/agent/tickets?status=Open`, `/agent/tickets?priority=Urgent`
- Customer: `/customer/tickets`, `/customer/tickets/new`, `/customer/tickets?status=Open`

The dashboard supports initial loading, manual Refresh, Retry on failures, zero-value statistics, and safe error messages. During refresh, existing statistics remain visible and duplicate refresh requests are ignored while a request is in flight. Forbidden errors render `You are not allowed to view these dashboard statistics.` Generic failures render `Dashboard statistics could not be loaded. Please try again.`

## Notifications

The authenticated shell renders a notification bell in the header. The login page and anonymous routes do not render the bell. Notification state is owned by the Angular notification state service and is not persisted in browser storage.

The frontend uses the official `@microsoft/signalr` package for live notification delivery.

Endpoints used:

| Purpose | Endpoint |
|---|---|
| Unread count | `GET /api/notifications/unread-count` |
| Notification list | `GET /api/notifications` |
| Mark one read | `PATCH /api/notifications/{id}/read` |
| Mark all read | `PATCH /api/notifications/read-all` |

SignalR contract:

| Purpose | Value |
|---|---|
| Hub route | `/hubs/notifications` |
| Local hub URL | `https://localhost:7194/hubs/notifications` derived from `environment.apiBaseUrl` |
| Production hub URL | `/hubs/notifications` when `apiBaseUrl` is empty and the app is same-origin |
| Event name | `NotificationReceived` |
| Payload | `NotificationResponse` |

When the authenticated layout is active, the frontend loads only the unread count initially. The full notification list is loaded lazily the first time the panel opens. Reopening an already loaded panel does not request the list again unless the user presses Refresh. Logout clears notification state, and a new login reloads the unread count.

The SignalR connection starts only after an authenticated user and token are available. It uses SignalR's `accessTokenFactory`, which reads the current token from `AuthStorageService`; tokens are never copied into components or logged. Logout, invalid auth, or user changes stop the current connection and clear notification state. A later login starts a clean connection. The service avoids starting another connection while one is connecting, connected, or reconnecting.

Automatic reconnect is configured with bounded delays: immediate, 2 seconds, 5 seconds, 10 seconds, and 30 seconds. SignalR connection failures do not block login, ticket pages, or REST notification behavior. The notification panel shows a subtle live-update state such as connected, reconnecting, connecting, or unavailable.

The bell accessible label includes the actual unread count, such as `Notifications, 3 unread`. Visual badges are capped at `99+`, while the accessible label keeps the real count.

Notification list items render backend fields as plain text: title, message, type, read/unread state, created local date/time, and an optional related-ticket action when `ticketId` exists. Recipient user IDs are not displayed.

When `NotificationReceived` arrives, the frontend validates the minimum payload shape, ignores malformed payloads, deduplicates by notification ID, inserts newest-first, and increments unread count only for unread notifications. Duplicate events do not duplicate items or increment counts twice. If a live notification arrives before the panel has loaded the REST list, it is retained and later merged with the REST response. REST list loading merges by ID instead of replacing state, so live notifications are not lost during in-flight list requests.

Marking one notification as read uses the backend response notification to update the loaded item and decrements the unread count without going below zero. Marking all as read updates loaded notifications to read and sets the unread count to zero. Duplicate mark-one and mark-all requests are ignored while the corresponding request is already in flight.

Related ticket navigation is role-aware:

- Admin: `/admin/tickets/{ticketId}`
- Agent: `/agent/tickets/{ticketId}`
- Customer: `/customer/tickets/{ticketId}`

If an unread notification has a related ticket, the frontend starts mark-as-read before navigating but does not block navigation on that request. Errors are shown safely:

- List: `Notifications could not be loaded. Please try again.`
- Mark one: `The notification could not be marked as read.`
- Mark all: `Notifications could not be updated. Please try again.`

Local development uses the backend's configured Angular CORS origin. Production should use HTTPS for the app/API origin, especially because browser SignalR transports may use the supported `access_token` mechanism during hub negotiation.

## Ticket Details

The shared ticket details page uses `GET /api/tickets/{id}` through the ticket data-access service.

| Role | Route |
|---|---|
| Admin | `/admin/tickets/:id` |
| Agent | `/agent/tickets/:id` |
| Customer | `/customer/tickets/:id` |

The details page renders backend-returned ticket metadata, including title, description, status, priority, category, source, customer fields, assignment, and UTC timestamps displayed in the browser's local timezone.

Conversation messages are rendered from the backend response only. The page defensively sorts messages by `createdAtUtc` ascending for timeline display, preserves line breaks as text, and never renders message bodies as HTML. Internal notes receive an explicit `Internal note` label and distinct styling only when the backend returns those messages. Customer visibility relies on backend authorization and filtering; the frontend does not display placeholders implying hidden internal notes exist.

Ticket list links include a validated internal `returnUrl` so the Back to tickets action can restore the previous filtered list query state. External or role-mismatched return URLs are rejected and fall back to the current role's list route.

## Ticket Creation

Customers create tickets at `/customer/tickets/new`. The route is protected with the Customer role guard and is linked from the Customer navigation as `Create Ticket`. Admin and Agent navigation does not expose a create-ticket link.

The page submits through `POST /api/tickets` using the centralized ticket data-access service. The current backend create contract accepts:

| Field | Frontend behavior |
|---|---|
| `title` | Required, trimmed before submit, maximum 200 characters |
| `description` | Required, trimmed before submit, maximum 4000 characters; line breaks are preserved |
| `customerEmail` | Sent as `null`; authenticated Customer defaults are handled by the backend |
| `customerName` | Sent as `null`; authenticated Customer defaults are handled by the backend |
| `source` | Sent as `Web` |

The create form does not expose server-managed fields such as status, assignment, owner ID, timestamps, or deletion state. It also does not expose priority or category because the current backend creation DTO does not accept those fields.

While submission is in flight, duplicate submits are blocked and the form is not cleared. On success, the page navigates with `replaceUrl` to `/customer/tickets/{createdTicketId}` using the ID returned by the backend. Cancel returns to `/customer/tickets`.

If the form is dirty and not successfully submitted, the route uses a `CanDeactivate` guard to confirm before leaving. Successful submission bypasses this warning. Create errors are displayed safely:

- `400`: `Please check the ticket details and try again.`
- `403`: `You are not allowed to create a ticket.`
- `409`: `The ticket could not be created because of a conflicting change.`
- generic failure: `The ticket could not be created. Please try again.`

### Conversation Actions

Public messages are submitted through `POST /api/tickets/{id}/messages` with `isInternalNote: false`. Admin, Agent, and Customer users see the public message composer. The backend remains authoritative for ticket ownership and assignment authorization.

Internal notes are submitted through `POST /api/tickets/{id}/internal-notes`. Only Admin and Agent users see the internal-note composer. Customers see no internal-note composer, warning, or placeholder. Backend authorization remains authoritative, so the UI handles `403` and `404` safely.

Both composers use Reactive Forms and enforce the backend maximum of 4000 characters. Message text is trimmed before submission, whitespace-only input is rejected, duplicate submissions are disabled while a request is in flight, and successful submissions append the returned message to the local conversation without a full refetch. Returned messages are deduplicated by ID and displayed chronologically.

### Workflow Actions

Admin assignment uses `PATCH /api/tickets/{id}/assign`. The frontend loads eligible Agents from `GET /api/users/agents` only for Admin users. The current backend assignment contract requires `assignedToUserId`, so unassignment is not offered.

Status changes use `PATCH /api/tickets/{id}/status`. Admin and Agent users see the status control; Customer users do not. The UI uses the backend enum values exactly:

- `Open`
- `InProgress`
- `WaitingForCustomer`
- `Resolved`
- `Closed`

Closing a ticket requires an explicit confirmation in the UI before the request is sent. The backend remains authoritative for assignment and status authorization, so rejected updates display safe messages without raw exception details.

Priority is read-only in the Angular UI because the backend currently has no priority-change endpoint.

### AI Assistant

Admin and Agent ticket-details views render an `AI assistant` section. Customer views show no AI controls, labels, hints, or placeholders. The backend remains authoritative, so an Agent can still receive a safe inaccessible-ticket message if the ticket is no longer assigned to them.

The Angular AI data-access service uses the centralized API service and these endpoints:

| Purpose | Endpoint | Optional request fields |
|---|---|---|
| Suggested customer reply | `POST /api/tickets/{id}/ai/suggest-reply` | `instruction` |
| Ticket summary | `POST /api/tickets/{id}/ai/summarize` | `includeInternalNotes` |
| Triage suggestion | `POST /api/tickets/{id}/ai/suggest-triage` | `instruction` |

Suggested-reply and triage instructions are trimmed before submit. Whitespace-only values are sent as `null`, and the client enforces the backend maximum of 500 characters. AI operations are never called automatically on page load and each operation has independent idle/loading/success/error state, so running one action does not disable the other AI actions, message composers, assignment, or status controls.

Suggested replies are draft-only. The user must choose `Insert into reply` to copy the AI text into the public reply composer, then press the normal Send button. The internal-note composer is never populated by AI suggestions. If the public composer already has text, the page asks before replacing that draft. Copy uses the browser Clipboard API through a small service and displays safe success or failure feedback without logging copied content.

Summaries are plain text and are not persisted. They exclude internal notes by default. Admin users may explicitly enable `Include internal notes`; the checkbox defaults to off and includes a staff-only warning. Agent users do not see that option and the frontend always sends `includeInternalNotes: false` for Agent summaries.

Triage suggestions are advisory only. The UI shows current priority, suggested priority, suggested category, escalation as textual Yes/No, optional escalation reason, and rationale. There is no Apply action, and the frontend does not change ticket priority, category, status, assignment, ownership, title, description, messages, notifications, SignalR events, or audit logs from AI output.

AI output is rendered only as text. The UI does not use `innerHTML`, Markdown rendering, or automatic persistence for generated content. Result areas preserve useful line breaks and wrap long text.

AI errors are normalized through `ApiError` and displayed without provider details, model names, local paths, stack traces, or raw backend exception text:

- `400`: `Please check the AI request and try again.`
- `403`: `You are not allowed to use this AI action.`
- `404`: `The ticket could not be found or you do not have access to it.`
- `429`: `Too many AI requests. Please wait before trying again.` If `Retry-After` is available, the message includes the wait time in seconds.
- `503`: `The AI service is temporarily unavailable. Please try again later.`
- Generic: `The AI request could not be completed. Please try again.`

# Local Development

## AI Provider Configuration

The API uses `IAiTicketAssistantService` for ticket triage and suggested customer replies. No paid or hosted AI provider is configured.

Rule-based AI is the safe default. The application uses it when:

- `AiAssistant` is missing.
- `AiAssistant:Provider` is missing or blank.
- `AiAssistant:Provider` is `RuleBased`.

Ollama is optional and explicitly opt-in. To use local Ollama, set:

```json
{
  "AiAssistant": {
    "Provider": "Ollama",
    "Ollama": {
      "BaseUrl": "http://localhost:11434",
      "Model": "llama3.2",
      "TimeoutSeconds": 15,
      "FallbackToRuleBased": true
    },
    "RateLimit": {
      "PermitLimit": 10,
      "WindowSeconds": 60
    }
  }
}
```

The public API contract does not change when switching providers. `POST /api/tickets/{id}/ai/suggest-reply` still returns:

```json
{
  "suggestedReply": "..."
}
```

`POST /api/tickets/{id}/ai/summarize` returns:

```json
{
  "summary": "..."
}
```

`POST /api/tickets/{id}/ai/suggest-triage` returns advisory recommendations only:

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

Ollama notes:

- Ollama is not contacted during application startup.
- Build and automated tests do not require Ollama, a downloaded model, or network access.
- The request timeout is controlled by `AiAssistant:Ollama:TimeoutSeconds`.
- When `FallbackToRuleBased` is `true`, Ollama failures, timeouts, non-success responses, and invalid or empty responses fall back to the rule-based provider.
- When `FallbackToRuleBased` is `false`, provider failures return a controlled API error without exposing connection details or provider internals.
- Internal notes are not included in suggested-reply AI input.
- Internal notes are not included in triage AI input.
- Prompts, ticket descriptions, ticket messages, generated replies, summaries, and triage suggestions are not logged by the application.
- AI ticket endpoints share a default per-authenticated-user rate limit of 10 requests per 60 seconds.
- Automated tests do not require Ollama, a downloaded model, network access, Redis, or any external monitoring service.

## Manual Ollama Smoke Test

Use this workflow only for local manual verification. Automated tests and normal builds must continue to run without Ollama.

### Prerequisites

1. Install or run Ollama outside this application. Follow the official Ollama installation instructions for your operating system.
2. Confirm the local Ollama service is reachable:

   ```powershell
   ollama list
   ```

   Or call the local API directly:

   ```powershell
   Invoke-RestMethod -Method Get -Uri "http://localhost:11434/api/tags"
   ```

3. Pull the model you want to test. The documented example model is `llama3.2`:

   ```powershell
   ollama pull llama3.2
   ```

4. Confirm the model is available:

   ```powershell
   ollama list
   ```

### Enable Ollama For Development

Keep the committed default as `RuleBased`. For a local smoke test, enable Ollama through `appsettings.Development.json`, user secrets, or environment variables.

Development configuration example:

```json
{
  "AiAssistant": {
    "Provider": "Ollama",
    "Ollama": {
      "BaseUrl": "http://localhost:11434",
      "Model": "llama3.2",
      "TimeoutSeconds": 30,
      "FallbackToRuleBased": false
    }
  }
}
```

Environment variable equivalent:

```powershell
$env:AiAssistant__Provider = "Ollama"
$env:AiAssistant__Ollama__BaseUrl = "http://localhost:11434"
$env:AiAssistant__Ollama__Model = "llama3.2"
$env:AiAssistant__Ollama__TimeoutSeconds = "30"
$env:AiAssistant__Ollama__FallbackToRuleBased = "false"
```

For the first smoke test, prefer `FallbackToRuleBased = false`. That way an unavailable Ollama instance returns the controlled `503` response instead of hiding the failure behind the deterministic rule-based reply.

Start the API in Development:

```powershell
dotnet run --project src\AiTicketing.Api\AiTicketing.Api.csproj --launch-profile https
```

The launch profile exposes the API on `https://localhost:7194` and `http://localhost:5143`.

### Authenticate

Use the existing auth endpoint to get a JWT. Register or log in as an Admin, or use an assigned Agent account.

Example login request:

```powershell
$loginBody = @{
  email = "admin@aiticketing.local"
  password = "P@ssw0rd!123"
} | ConvertTo-Json

$auth = Invoke-RestMethod `
  -Method Post `
  -Uri "https://localhost:7194/api/auth/login" `
  -ContentType "application/json" `
  -Body $loginBody

$token = $auth.data.token
```

If demo users are not seeded in your local database, register an Admin through `POST /api/auth/register`, then log in with that account. Do not put real passwords or tokens in source control.

### Create Or Select A Ticket

Select an existing non-deleted ticket that the authenticated Admin can access. For an Agent smoke test, choose a ticket assigned to that Agent.

If needed, create a ticket through the existing public endpoint:

```powershell
$ticketBody = @{
  title = "Payment page is not working"
  description = "The customer cannot complete payment and receives an error every time."
  customerEmail = "customer@example.com"
  customerName = "Sara Ahmed"
  source = "Web"
} | ConvertTo-Json

$createdTicket = Invoke-RestMethod `
  -Method Post `
  -Uri "https://localhost:7194/api/tickets" `
  -ContentType "application/json" `
  -Body $ticketBody

$ticketId = $createdTicket.data.ticket.id
```

### Run The Suggested Reply Smoke Test

Call the existing endpoint. The public request and response contract is unchanged.

```powershell
$suggestBody = @{
  instruction = "Keep the reply friendly and concise."
} | ConvertTo-Json

$suggestion = Invoke-RestMethod `
  -Method Post `
  -Uri "https://localhost:7194/api/tickets/$ticketId/ai/suggest-reply" `
  -Headers @{ Authorization = "Bearer $token" } `
  -ContentType "application/json" `
  -Body $suggestBody

$suggestion.data
```

Expected response shape:

```json
{
  "suggestedReply": "..."
}
```

You can also send an empty body:

```powershell
Invoke-RestMethod `
  -Method Post `
  -Uri "https://localhost:7194/api/tickets/$ticketId/ai/suggest-reply" `
  -Headers @{ Authorization = "Bearer $token" } `
  -ContentType "application/json"
```

Success checklist:

- The response contains only `suggestedReply` inside the standard API response wrapper.
- The reply is not the deterministic rule-based text that starts with `Thanks for contacting us about`.
- No Ollama metadata is returned.
- No ticket message is created.
- No notification is created.
- No SignalR event is sent.
- No audit log is created.
- Ticket status is unchanged.

Implementation checklist verified by automated tests and code inspection:

- Ollama is contacted only when `AiAssistant:Provider` is `Ollama`.
- The configured model is sent in the Ollama request.
- `stream` is `false`.
- Title, description, status, priority, optional trimmed instruction, and non-internal messages are included in the prompt.
- Internal notes are excluded from the prompt.
- Generated reply text is trimmed before returning.
- Provider errors do not expose stack traces, local paths, raw response bodies, or provider internals to the API client.

### Run The Ticket Summary Smoke Test

Use the same authenticated Admin or assigned Agent token and ticket ID:

```powershell
$summaryBody = @{
  includeInternalNotes = $false
} | ConvertTo-Json

$summary = Invoke-RestMethod `
  -Method Post `
  -Uri "https://localhost:7194/api/tickets/$ticketId/ai/summarize" `
  -Headers @{ Authorization = "Bearer $token" } `
  -ContentType "application/json" `
  -Body $summaryBody

$summary.data
```

Expected response shape:

```json
{
  "summary": "..."
}
```

Admin-only internal-note smoke test:

```powershell
$summaryWithInternalNotesBody = @{
  includeInternalNotes = $true
} | ConvertTo-Json

Invoke-RestMethod `
  -Method Post `
  -Uri "https://localhost:7194/api/tickets/$ticketId/ai/summarize" `
  -Headers @{ Authorization = "Bearer $token" } `
  -ContentType "application/json" `
  -Body $summaryWithInternalNotesBody
```

Assigned Agents should receive `403` if they set `includeInternalNotes` to `true`. Summary generation is read-only: it does not create messages, notifications, SignalR events, audit logs, or ticket state changes.

### Run The Triage Suggestion Smoke Test

Use an authenticated Admin or assigned Agent token and a non-deleted ticket ID:

```powershell
$triageBody = @{
  instruction = "Focus on whether this requires urgent escalation."
} | ConvertTo-Json

$triage = Invoke-RestMethod `
  -Method Post `
  -Uri "https://localhost:7194/api/tickets/$ticketId/ai/suggest-triage" `
  -Headers @{ Authorization = "Bearer $token" } `
  -ContentType "application/json" `
  -Body $triageBody

$triage.data
```

Expected response shape:

```json
{
  "currentPriority": "Medium",
  "suggestedPriority": "High",
  "suggestedCategory": "Billing",
  "escalationRecommended": true,
  "escalationReason": "...",
  "rationale": "..."
}
```

Triage generation is advisory and read-only: it does not update priority, category, status, assignment, ownership, title, description, messages, notifications, SignalR events, or audit logs. Internal notes are excluded. If Ollama is unavailable and `FallbackToRuleBased` is `true`, the deterministic rule-based provider returns a conservative suggestion.

### Run The AI Rate-Limit Smoke Test

To test 429 behavior locally without waiting, temporarily override:

```json
{
  "AiAssistant": {
    "RateLimit": {
      "PermitLimit": 2,
      "WindowSeconds": 60
    }
  }
}
```

Then call any combination of AI endpoints three times with the same Admin or assigned Agent token:

```powershell
1..3 | ForEach-Object {
  try {
    Invoke-RestMethod `
      -Method Post `
      -Uri "https://localhost:7194/api/tickets/$ticketId/ai/suggest-reply" `
      -Headers @{ Authorization = "Bearer $token" } `
      -ContentType "application/json" `
      -Body "{}"
  }
  catch {
    $_.Exception.Response.StatusCode
    $_.Exception.Response.Headers["Retry-After"]
  }
}
```

The third call should return `429 Too Many Requests` with the standard API response wrapper and a safe message. The quota is shared across `suggest-reply`, `summarize`, and `suggest-triage` for the same authenticated user. Anonymous users still receive `401`; they do not bypass authentication through rate limiting.

### Fallback Smoke Test

Use this to verify the safe fallback path:

1. Set `AiAssistant:Provider` to `Ollama`.
2. Set `AiAssistant:Ollama:FallbackToRuleBased` to `true`.
3. Stop Ollama or point `AiAssistant:Ollama:BaseUrl` at an unavailable local address.
4. Call `POST /api/tickets/{id}/ai/suggest-reply`.
5. Confirm the request succeeds and returns the deterministic rule-based style reply.
6. Confirm there is no long retry loop. The implementation performs a single Ollama attempt per suggestion.

### Controlled 503 Smoke Test

Use this to verify provider failures are surfaced safely:

1. Set `AiAssistant:Provider` to `Ollama`.
2. Set `AiAssistant:Ollama:FallbackToRuleBased` to `false`.
3. Stop Ollama or point `AiAssistant:Ollama:BaseUrl` at an unavailable local address.
4. Call `POST /api/tickets/{id}/ai/suggest-reply`.
5. Confirm the API returns `503 Service Unavailable`.
6. Confirm the response uses the standard API wrapper with a safe message such as `AI provider is unavailable.`
7. Confirm the response does not expose stack traces, URLs, local paths, raw Ollama responses, or connection internals.

### Return To Default RuleBased Provider

Remove the local Ollama overrides or set:

```json
{
  "AiAssistant": {
    "Provider": "RuleBased"
  }
}
```

After returning to `RuleBased`, the API should not contact Ollama for suggested replies.

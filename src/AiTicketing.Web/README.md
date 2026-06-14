# AiTicketing.Web

Angular frontend foundation for AiTicketingAssistant. It uses Angular 19.1.6, standalone components, Angular Router, HttpClient, Reactive Forms, RxJS, and Angular Signals for local authentication state.

## Prerequisites

- Node.js 22.x or a compatible active LTS version
- Angular CLI 19.1.6 or the local `npm run ng` command after dependencies are installed
- The ASP.NET Core backend running locally

## Install

```bash
npm install
```

## API Base URL

Development uses `src/environments/environment.ts`:

```ts
apiBaseUrl: 'https://localhost:7194'
```

Production uses `src/environments/environment.production.ts`. Set the production API URL there or deploy the frontend behind the same origin and keep it as an empty string.

Do not put secrets in Angular environment files. They are bundled into browser assets.

## Local Development

Backend default URL:

```text
https://localhost:7194
```

Frontend dev URL:

```text
http://localhost:4200
```

Run the Angular dev server:

```bash
npm start
```

The backend has a Development-only CORS policy for `http://localhost:4200`.

## Authentication

The backend returns a JWT to browser JavaScript. This first frontend foundation stores the token in `sessionStorage` through `AuthStorageService`; components must not access `sessionStorage` directly. This is intentionally isolated so the storage strategy can later move to a safer server-managed cookie flow if the backend adds it.

After login, the app calls `GET /api/me` and uses that response for current-user identity and roles. It does not decode the JWT as the primary authorization source.

## Commands

```bash
npm run typecheck
npm test
npm run build
```

`npm test` runs Karma once with Chrome Headless.

## Current Scope

Implemented now:

- Login page
- Auth state restoration
- Token interceptor
- Auth and role guards
- Admin, Agent, and Customer placeholder shells

Not implemented yet:

- Ticket screens
- Notification screens
- SignalR client
- AI workflow screens

# AI Ticketing Assistant

A bilingual, AI-powered helpdesk and ticket management platform for administrators, support agents, and customers.

Built with **ASP.NET Core**, **Angular**, **SQL Server**, **SignalR**, and **Ollama-compatible AI integration**.

The application supports English and Arabic, including full right-to-left layout support, responsive desktop and mobile interfaces, real-time notifications, role-based workflows, and AI-assisted ticket operations.

---

## Table of Contents

* [About](#about)
* [Features](#features)
* [User Roles](#user-roles)
* [AI Capabilities](#ai-capabilities)
* [Screenshots](#screenshots)
* [Technology Stack](#technology-stack)
* [Architecture](#architecture)
* [Project Structure](#project-structure)
* [Prerequisites](#prerequisites)
* [Getting Started](#getting-started)
* [Configuration](#configuration)
* [Database Setup](#database-setup)
* [Running the Application](#running-the-application)
* [Running Tests](#running-tests)
* [Production Build](#production-build)
* [Localization and RTL](#localization-and-rtl)
* [Real-Time Notifications](#real-time-notifications)
* [Security Notes](#security-notes)
* [API Documentation](#api-documentation)
* [Development Notes](#development-notes)
* [Deployment Checklist](#deployment-checklist)
* [Roadmap](#roadmap)
* [Contributing](#contributing)
* [License](#license)
* [Arabic Overview](#arabic-overview)

---

## About

**AI Ticketing Assistant** is a modern helpdesk platform designed to simplify customer-support workflows.

It allows customers to create and follow support tickets, support agents to manage conversations and internal notes, and administrators to oversee assignment, ticket status, notifications, and support operations.

The platform also includes AI-assisted tools for:

* suggested customer replies
* ticket summaries
* triage recommendations
* priority and category suggestions
* escalation guidance

AI-generated results are advisory only. Suggested replies are never sent automatically, and AI tools do not modify ticket data without explicit user action.

---

## Features

### Ticket Management

* Create and view support tickets
* Search and filter tickets
* Filter by status, priority, category, and assigned agent
* Sort and paginate ticket results
* Assign tickets to active support agents
* Update ticket status
* Track ticket metadata and activity
* Responsive desktop table and mobile card layouts

### Conversations

* Public customer and staff replies
* Internal staff-only notes
* Role and sender information
* Localized timestamps
* Preserved line breaks
* Mixed Arabic and English text support
* Clear visual distinction between public replies and internal notes

### Dashboards

Role-specific dashboards for:

* Administrators
* Support Agents
* Customers

Dashboard metrics include:

* Total tickets
* Open tickets
* Tickets in progress
* Urgent tickets
* Resolved tickets
* Closed tickets
* Unassigned tickets

### Notifications

* Real-time notifications using SignalR
* Unread notification count
* Mark individual notifications as read
* Mark all notifications as read
* Open related tickets
* Connected, reconnecting, and unavailable states
* Responsive desktop popover and mobile-friendly presentation

### User Experience

* English and Arabic localization
* Full RTL support
* Light theme
* Dark theme
* System theme preference
* Persistent language and theme settings
* Responsive layouts
* Keyboard-accessible menus and drawers
* Accessible focus handling
* Mobile navigation drawer
* Safe loading, empty, and error states

---

## User Roles

### Administrator

Administrators can:

* View all tickets
* View ticket statistics
* Assign tickets to active agents
* Update ticket workflows
* Add public replies
* Add internal notes
* Use AI reply, summary, and triage tools
* View and manage notifications

### Support Agent

Support agents can:

* View assigned or accessible tickets
* Reply to customers
* Add internal notes
* Update supported ticket workflows
* Use AI-assisted tools
* Receive real-time notifications

### Customer

Customers can:

* Create tickets
* View their own tickets
* Read public conversations
* Reply to their tickets
* Track ticket status

Customers cannot access:

* Internal notes
* Staff-only workflows
* Assignment controls
* AI tools

---

## AI Capabilities

The AI Assistant is available only to authorized staff.

### Suggested Reply

Generates a draft response based on the ticket context and an optional instruction.

Behavior:

* trims user instructions
* treats whitespace-only instructions as empty
* limits instruction length
* renders AI output as plain text
* supports copying
* supports inserting into the public reply composer
* never sends the reply automatically
* never inserts content into the internal-note composer
* asks before replacing an existing reply draft

### Ticket Summary

Creates a concise summary of the ticket conversation.

Administrators may optionally include internal notes.

Support Agents always request summaries without internal notes.

### Triage Recommendation

Provides advisory recommendations such as:

* current priority
* suggested priority
* suggested category
* escalation recommendation
* escalation reason
* rationale

Triage recommendations are advisory only and do not include an automatic Apply action.

---

## Screenshots

Add production-ready screenshots to a version-controlled folder such as:

```text
docs/images/
```

Suggested screenshots:

* Login
* Admin dashboard
* Ticket list
* Ticket details
* AI Assistant
* Notifications
* Arabic RTL view
* Mobile ticket list
* Dark mode

Example:

```md
![Admin Dashboard](docs/images/admin-dashboard.png)
![Ticket Details](docs/images/ticket-details.png)
![Arabic RTL](docs/images/dashboard-arabic-rtl.png)
```

Do not commit temporary browser profiles or visual-QA artifacts.

---

## Technology Stack

### Backend

* ASP.NET Core
* C#
* Entity Framework Core
* SQL Server
* ASP.NET Core Identity
* JWT authentication
* SignalR
* FluentValidation
* Swagger / OpenAPI

### Frontend

* Angular
* TypeScript
* SCSS
* Standalone Angular components
* Angular Router
* Angular reactive forms

### AI

* Ollama-compatible local AI endpoints
* Server-side AI orchestration
* Safe advisory-only frontend behavior

### Testing

* .NET test suite
* Angular unit and component tests
* ChromeHeadless browser tests
* Focused workflow and localization tests

---

## Architecture

The backend follows a layered architecture:

```text
API
│
├── Application
├── Domain
└── Infrastructure
```

### API Layer

Responsible for:

* HTTP endpoints
* authentication and authorization
* middleware
* OpenAPI documentation
* response wrapping
* SignalR hubs

### Application Layer

Contains:

* contracts
* request and response models
* validation rules
* service abstractions
* application-level workflows

### Domain Layer

Contains:

* core entities
* enums
* business concepts
* domain rules

### Infrastructure Layer

Contains:

* Entity Framework Core
* SQL Server persistence
* Identity implementation
* ticket services
* user lookup
* notifications
* SignalR delivery
* AI integration
* database seeding

### Angular Frontend

Organized into:

* core services
* authentication
* layouts
* feature modules
* shared components
* shared pipes
* localization
* responsive UI components

---

## Project Structure

```text
AiTicketingAssistant/
├── docs/
├── src/
│   ├── AiTicketing.Api/
│   ├── AiTicketing.Application/
│   ├── AiTicketing.Domain/
│   ├── AiTicketing.Infrastructure/
│   └── AiTicketing.Web/
├── tests/
│   └── AiTicketing.Tests/
├── .gitignore
├── AiTicketingAssistant.slnx
└── README.md
```

Frontend structure:

```text
src/AiTicketing.Web/src/app/
├── core/
│   ├── auth/
│   ├── localization/
│   └── services/
├── features/
│   ├── auth/
│   ├── dashboard/
│   ├── notifications/
│   └── tickets/
├── layouts/
├── shared/
└── app.routes.ts
```

---

## Prerequisites

Install the following tools before running the project:

* .NET SDK
* Node.js
* npm
* SQL Server
* Angular-compatible browser
* Ollama or another compatible local AI service, when AI features are enabled

Recommended development tools:

* Visual Studio
* Visual Studio Code
* SQL Server Management Studio
* Git

---

## Getting Started

Clone the repository:

```bash
git clone <repository-url>
cd AiTicketingAssistant
```

Restore backend dependencies:

```bash
dotnet restore
```

Install frontend dependencies:

```bash
cd src/AiTicketing.Web
npm install
cd ../../..
```

---

## Configuration

### Backend Configuration

Configure development settings using:

```text
src/AiTicketing.Api/appsettings.Development.json
```

Do not commit real production secrets.

Use environment variables or .NET User Secrets for:

* SQL Server connection strings
* JWT signing keys
* AI service URLs
* API keys
* production frontend origin

Example environment-variable names:

```text
ConnectionStrings__DefaultConnection
Jwt__Key
Jwt__Issuer
Jwt__Audience
Cors__AllowedOrigins__0
Ai__BaseUrl
```

### User Secrets

From the API project directory:

```bash
cd src/AiTicketing.Api
dotnet user-secrets init
```

Add a development connection string:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<connection-string>"
```

Add a JWT signing key:

```bash
dotnet user-secrets set "Jwt:Key" "<strong-development-key>"
```

Never use development secrets in production.

### Frontend API URL

Configure the Angular API base URL according to the project environment files or application configuration.

Development example:

```text
https://localhost:7194
```

Production example:

```text
https://api.example.com
```

Use the actual environment structure already defined in the Angular project.

---

## Database Setup

Ensure SQL Server is running and the connection string is valid.

Apply migrations:

```bash
dotnet ef database update \
  --project src/AiTicketing.Infrastructure \
  --startup-project src/AiTicketing.Api
```

If `dotnet ef` is not installed:

```bash
dotnet tool install --global dotnet-ef
```

The application may seed required roles during startup.

Review all seed data before deploying to production.

Do not deploy default demonstration passwords or accounts unless they are explicitly intended and secured.

### Local SQL Server Encryption

Some local SQL Server installations may require development-only connection-string options such as:

```text
TrustServerCertificate=True
```

or, when appropriate for local development only:

```text
Encrypt=False
```

Choose the option that matches your SQL Server configuration.

Do not weaken production database security without understanding the hosting environment.

---

## Running the Application

### Start the Backend

From the repository root:

```bash
dotnet run --project src/AiTicketing.Api/AiTicketing.Api.csproj
```

The API development URL is typically similar to:

```text
https://localhost:7194
```

Check the console output for the exact address.

### Start the Frontend

Open another terminal:

```bash
cd src/AiTicketing.Web
npm start
```

The Angular application is usually available at:

```text
http://localhost:4200
```

### Start the AI Service

Start Ollama or the configured compatible AI service.

Example:

```bash
ollama serve
```

Ensure the required model is installed and configured according to the backend AI settings.

---

## Running Tests

### Backend Tests

From the repository root:

```bash
dotnet test
```

Run a focused test filter:

```bash
dotnet test --filter "FullyQualifiedName~Tickets"
```

### Frontend Type Checking

```bash
cd src/AiTicketing.Web
npm run typecheck
```

### Frontend Tests

```bash
npm test -- --watch=false --browsers=ChromeHeadless
```

### Frontend Production Build

```bash
npm run build
```

---

## Production Build

Build the backend:

```bash
dotnet publish src/AiTicketing.Api/AiTicketing.Api.csproj \
  --configuration Release \
  --output ./publish/api
```

Build the Angular frontend:

```bash
cd src/AiTicketing.Web
npm run build
```

The Angular output is generated under the project’s configured `dist` directory.

Before deployment, verify:

* production API URL
* production CORS origin
* HTTPS
* JWT configuration
* SQL Server connection
* database migrations
* logging
* AI service availability
* SignalR connectivity

---

## Localization and RTL

The application supports:

* English
* Arabic
* LTR layout
* RTL layout

The selected language is persisted in browser storage.

The application updates:

* `html lang`
* `html dir`
* localized labels
* date and time formatting
* navigation direction
* directional icons
* drawer position
* notification anchoring

User-generated and backend-provided content is not automatically translated.

Mixed-language content uses direction isolation where appropriate.

Examples:

* ticket titles use automatic direction
* message bodies use automatic direction
* email addresses remain LTR
* technical identifiers remain LTR
* credential inputs remain LTR

---

## Real-Time Notifications

SignalR is used to deliver real-time notification updates.

The frontend supports:

* connected state
* reconnecting state
* unavailable state
* unread count updates
* notification refresh
* related-ticket navigation

SignalR delivery should not expose sensitive data.

Review production proxy and hosting configuration to ensure WebSocket support is enabled.

---

## Security Notes

Before production deployment:

* store secrets outside source control
* use a strong JWT signing key
* configure short and appropriate token lifetimes
* restrict CORS to trusted frontend origins
* enforce HTTPS
* review role-based authorization
* validate all client input
* avoid returning raw exception details
* enable production logging
* consider rate limiting for login and AI endpoints
* review account lockout settings
* rotate exposed development tokens or keys
* review database permissions
* remove insecure demo credentials
* verify internal notes are staff-only
* verify customers cannot access staff or AI workflows

CORS is not a replacement for authentication or authorization.

---

## API Documentation

Swagger/OpenAPI is available in development when enabled by the API configuration.

Typical URL:

```text
https://localhost:7194/swagger
```

Key ticket endpoints include workflows such as:

```http
GET    /api/tickets
GET    /api/tickets/{id}
POST   /api/tickets
PATCH  /api/tickets/{id}/assign
```

Assignment request body:

```json
{
  "assignedToUserId": "00000000-0000-0000-0000-000000000000"
}
```

AI endpoints include:

```http
POST /api/tickets/{id}/ai/suggest-reply
POST /api/tickets/{id}/ai/summarize
POST /api/tickets/{id}/ai/suggest-triage
```

Suggested reply request:

```json
{
  "instruction": "Keep the response concise and professional."
}
```

Summary request:

```json
{
  "includeInternalNotes": false
}
```

Triage request:

```json
{
  "instruction": "Focus on payment-related urgency."
}
```

Refer to Swagger and the project documentation for the complete API contract.

---

## Development Notes

Additional project documentation may be available under:

```text
docs/
```

Examples:

* frontend development guide
* frontend API contract
* local development guide
* SignalR notification guide

Temporary files should not be committed:

```gitignore
artifacts/
.codex-chrome-profile-*
```

Also ignore:

* local database files
* production secrets
* browser profiles
* build output
* test output
* local logs

---

## Deployment Checklist

Before creating a release:

* [ ] Backend build passes
* [ ] Backend tests pass
* [ ] Frontend typecheck passes
* [ ] Frontend tests pass
* [ ] Frontend production build passes
* [ ] Database migrations are reviewed
* [ ] Production connection string is configured securely
* [ ] JWT keys are stored securely
* [ ] CORS allows only trusted origins
* [ ] HTTPS is enabled
* [ ] SignalR works through the production proxy
* [ ] AI service is reachable
* [ ] English interface is verified
* [ ] Arabic RTL interface is verified
* [ ] Mobile layouts are verified
* [ ] Dark and light themes are verified
* [ ] Role permissions are verified
* [ ] Error responses do not expose stack traces
* [ ] Logs and monitoring are configured
* [ ] Temporary artifacts are excluded from Git
* [ ] README and screenshots are updated

---

## Roadmap

Possible future improvements:

* Email ingestion
* File attachments
* SLA policies
* Advanced reporting
* Customer satisfaction ratings
* Saved filters
* Ticket tags
* Knowledge-base integration
* Additional languages
* Production monitoring dashboards
* Background job processing
* AI model configuration from an administrator interface

These features are not part of the current implementation unless explicitly added later.

---

## Contributing

Contributions are welcome.

Recommended workflow:

1. Fork the repository
2. Create a feature branch

```bash
git checkout -b feature/your-feature
```

3. Make focused changes
4. Add or update tests
5. Run backend and frontend verification
6. Commit your changes

```bash
git commit -m "Add your feature"
```

7. Push the branch

```bash
git push origin feature/your-feature
```

8. Open a pull request

Please avoid committing:

* secrets
* build output
* browser profiles
* generated QA artifacts
* local database files

---

## License

No license has been selected yet.

Before publishing the repository for public reuse, add a `LICENSE` file and update this section.

Example options:

* MIT
* Apache License 2.0
* Proprietary / All Rights Reserved

---

## Arabic Overview

# مساعد التذاكر الذكي

**مساعد التذاكر الذكي** هو نظام ثنائي اللغة لإدارة تذاكر الدعم الفني، مصمم للمسؤولين وموظفي الدعم والعملاء.

يوفر النظام:

* إنشاء التذاكر ومتابعتها
* تعيين التذاكر لموظفي الدعم
* تغيير حالة التذكرة
* الردود العامة
* الملاحظات الداخلية للموظفين
* الإشعارات الفورية باستخدام SignalR
* اقتراح الردود بالذكاء الاصطناعي
* تلخيص محادثة التذكرة
* اقتراح الأولوية والتصنيف والتصعيد
* الوضع الفاتح والداكن ووضع النظام
* دعم العربية والإنجليزية
* دعم كامل لاتجاه الواجهة من اليمين إلى اليسار
* تصميم متجاوب لسطح المكتب والجوال

نتائج الذكاء الاصطناعي إرشادية فقط.

الردود المقترحة لا تُرسل تلقائيًا، ولا تقوم أدوات الذكاء الاصطناعي بتغيير بيانات التذكرة دون إجراء صريح من المستخدم.

## تشغيل المشروع محليًا

تشغيل الواجهة الخلفية:

```bash
dotnet restore
dotnet build
dotnet run --project src/AiTicketing.Api/AiTicketing.Api.csproj
```

تشغيل الواجهة الأمامية:

```bash
cd src/AiTicketing.Web
npm install
npm start
```

تشغيل اختبارات الواجهة الخلفية:

```bash
dotnet test
```

تشغيل اختبارات Angular:

```bash
cd src/AiTicketing.Web
npm run typecheck
npm test -- --watch=false --browsers=ChromeHeadless
npm run build
```


# Architecture

## Stack

| Layer | Choice | Why |
|---|---|---|
| Runtime | .NET 10 (LTS) | One language for web, API and mobile; supported until Nov 2028 |
| Web UI | Blazor Web App | Public pages render as static HTML (fast, indexable, no JS needed). Portal pages run interactively over a Blazor Server circuit for live chat and status updates |
| API | ASP.NET Core Minimal APIs | Same services as the UI, consumed by the future .NET MAUI app |
| Auth | ASP.NET Core Identity | Cookie for the website, bearer tokens for the API, one user store |
| Data | EF Core 10 — SQLite (dev/test), PostgreSQL (prod) | Zero-setup locally, robust in production |
| Payments | Stripe Checkout + webhooks over HTTPS | Hosted payment page keeps card data off our servers (PCI SAQ A) |
| AI | Claude API (optional) | Turns rough client descriptions into structured briefs |
| Mobile (Phase 2) | .NET MAUI | iOS + Android from C#, against `/api` |

## Layout

```
src/Novaforge.Web
  Domain/         Entities, enums, display text — no framework logic
  Data/           AppDbContext, schema config, startup migration + seeding
  Services/       Business logic used by both UI and API (projects, invoices, leads,
                  payments, AI, e-mail, background jobs). Every method takes a CurrentUser
                  and enforces ownership itself.
  Endpoints/      JSON API, Stripe webhook, logout, SEO files, security headers
  Components/
    Pages/        Public pages (static SSR)
    Account/      Sign in / register (static SSR forms)
    Portal/       Client portal (interactive)
    Admin/        Admin screens (interactive, Admin role)
    Layout/, Shared/
  wwwroot/        CSS design system, fonts, one small JS file
tests/Novaforge.Tests
  UnitTests.cs    Pure logic
  ApiTests.cs     In-memory app with SQLite + fakes
```

## Key flows

**Project request.** Client fills the form → `ProjectService.CreateAsync` validates, stores the project and a timeline
event, e-mails the team. The team replies in the thread or changes status; `ProjectNotifier` pushes the change to every
open portal page for that project, so both sides see it without refreshing.

**Payment.** Admin issues an invoice (amounts in minor units, never floats) → client clicks *Pay* →
`StripePaymentGateway` creates a Checkout Session with the invoice id in metadata → Stripe redirects back →
Stripe calls `/api/webhooks/stripe` → signature and timestamp are verified, the event id is recorded for idempotency,
the invoice is marked paid and both sides are notified.

**Access control.** Pages use `[Authorize]` / `[Authorize(Roles = "Admin")]`; services re-check ownership on every
query and return *not found* for other people's data so ids can't be probed.

## Scaling notes

- `ProjectNotifier` is in-process. With more than one app instance, replace it with Redis pub/sub and enable sticky
  sessions (or Azure SignalR Service) for Blazor circuits.
- Data-protection keys must be shared between instances (`DataProtection:KeysPath` on a shared volume, or Azure Blob/Redis).
- The overdue-reminder job runs in every instance; with several instances move it to a single worker or add a DB lock.

# Novaforge

A complete .NET 10 platform for a software studio: a fast, SEO-friendly marketing site, a client portal
where customers submit projects and bug reports, chat with the team, follow progress live and pay invoices,
and a JSON API ready for the iOS/Android app.

> "Novaforge" is a placeholder brand. Change `Site:Name` in `appsettings.json` (or `SITE_NAME` in `.env`).

## What's inside

| Area | Features |
|---|---|
| Public website | Home, 12 service pages, process, contact/quote form with spam honeypot, privacy & terms, sitemap.xml, robots.txt, Open Graph tags, dark/light theme, glass UI, self-hosted fonts (no third-party requests) |
| Client portal | Sign up / sign in, dashboard, new project or bug report (with optional AI brief), project page with live chat and activity timeline, invoices with card payment and print/PDF |
| Admin | Enquiries inbox, review queue, status updates with client notes, issue / void / refund invoices |
| API (`/api`) | Bearer-token auth (`/api/auth/register`, `/login`, `/refresh`), projects, messages, invoices, checkout, leads, AI brief, Stripe webhook. OpenAPI at `/openapi/v1.json` in Development |
| Payments | Stripe Checkout (card data never touches the server), signed + idempotent webhooks, partial and full refunds, overdue reminders |
| Security | ASP.NET Core Identity, lockout, role-based access, strict CSP and security headers, HSTS, rate limiting, antiforgery, owner checks on every query |
| Ops | Health check `/health`, Dockerfile (non-root), docker-compose with PostgreSQL, GitHub Actions CI, Dependabot |

## Run it locally (Visual Studio or CLI)

Requirements: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). Nothing else — local dev uses SQLite.

```bash
dotnet run --project src/Novaforge.Web
```

Open https://localhost:7180 and sign in with the dev admin from `appsettings.Development.json`
(`admin@novaforge.local` / `ChangeMe-Dev-123`). Register a second account to see the client side.

In Visual Studio 2026: open `Novaforge.slnx`, set **Novaforge.Web** as startup project, press F5.

## Tests

```bash
dotnet test
```

Unit tests cover webhook signature checks, money maths and validation. Integration tests boot the real app
in memory (SQLite, faked e-mail and Stripe) and exercise pages, auth, access control, projects, invoices,
checkout, webhooks and refunds.

## Configuration

All settings live in `appsettings.json` and can be overridden with environment variables
(`Section__Key`, e.g. `Stripe__SecretKey`). Keep secrets out of git: use `dotnet user-secrets` locally
and your host's secret store in production.

| Setting | Purpose |
|---|---|
| `Site:Name`, `Site:BaseUrl`, `Site:ContactEmail`, `Site:NotificationEmail` | Branding, canonical URLs, Stripe return URLs, where alerts go |
| `Database:Provider` | `Sqlite` (default) or `Postgres` |
| `ConnectionStrings:Default` | Database connection |
| `Admin:Email`, `Admin:Password` | First admin, created at startup if missing |
| `Stripe:SecretKey`, `Stripe:WebhookSecret`, `Stripe:DefaultCurrency` | Card payments (optional) |
| `Ai:ApiKey`, `Ai:Model` | AI project briefs via the Claude API (optional) |
| `Smtp:*` | Outgoing e-mail (optional — logged when not set) |
| `DataProtection:KeysPath` | Persist auth keys across restarts (set in Docker) |
| `RateLimits:PublicPerMinute`, `RateLimits:AuthPerMinute` | Abuse protection |

```bash
cd src/Novaforge.Web
dotnet user-secrets set "Stripe:SecretKey" "sk_test_..."
dotnet user-secrets set "Stripe:WebhookSecret" "whsec_..."
dotnet user-secrets set "Ai:ApiKey" "sk-ant-..."
```

## Docker

```bash
cp .env.example .env    # fill in the values
docker compose up --build
```

Open http://localhost:8080.

## More docs

- [Architecture](docs/ARCHITECTURE.md)
- [Deployment guide](docs/DEPLOYMENT.md)
- [Progress checklist & roadmap](docs/CHECKLIST.md)

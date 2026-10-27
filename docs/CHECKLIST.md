# Progress checklist

## Phase 1 — Platform foundation (this delivery)

- [x] .NET 10 solution, Blazor Web App, Minimal API, EF Core (SQLite dev / PostgreSQL prod)
- [x] Design system: glass panes, blueprint grid, dark/light theme, self-hosted Inter fonts, reduced-motion support
- [x] Public site: home, 12 service pages, process, contact/quote form, privacy, terms, 404/error pages
- [x] SEO: titles, descriptions, canonical URLs, Open Graph, sitemap.xml, robots.txt, semantic HTML
- [x] Accounts: register, sign in, lockout, sign out, roles (Admin / Client)
- [x] Client portal: dashboard, requests (new build, bug fix, upgrade…), live chat, activity timeline
- [x] Admin: enquiries inbox, review queue, status updates, invoices (issue, void, refund)
- [x] Stripe Checkout, signed idempotent webhooks, refunds, overdue reminders
- [x] AI project brief (Claude API, optional)
- [x] Security headers (CSP, HSTS, frame, referrer), rate limiting, antiforgery, honeypot
- [x] JSON API with bearer tokens for mobile
- [x] Unit + integration tests
- [x] Dockerfile, docker-compose, GitHub Actions CI, Dependabot
- [ ] **First CI run green on GitHub** (code was written in an environment without the .NET SDK — CI is the first compile)
- [ ] Generate EF migrations (see DEPLOYMENT.md)
- [ ] Real brand name, domain, legal texts

## Phase 2 — Mobile & growth

- [ ] .NET MAUI app (iOS + Android): sign in, projects, chat, invoices, push notifications
- [ ] E-mail confirmation and password reset pages
- [ ] Two-factor authentication
- [ ] File uploads on projects (screenshots, documents) to blob storage
- [ ] Portfolio / case studies section (CMS-managed)
- [ ] Subscriptions for maintenance retainers (Stripe Billing)
- [ ] PayPal / MobilePay as additional payment methods

## Phase 3 — Scale & automation

- [ ] Redis backplane for live updates across instances
- [ ] OpenTelemetry tracing and dashboards
- [ ] AI triage of incoming bug reports, semantic search across projects
- [ ] End-to-end browser tests (Playwright for .NET)
- [ ] Multi-language site (Danish / English)

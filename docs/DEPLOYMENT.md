# Deployment guide

## 1. Before the first deploy

1. Pick your brand name and domain; set `Site:Name` and `Site:BaseUrl` (e.g. `https://yourstudio.dk`).
2. Create database migrations (recommended for production, so future schema changes are safe):
   ```bash
   dotnet tool install --global dotnet-ef
   cd src/Novaforge.Web
   dotnet ef migrations add Initial --output-dir Data/Migrations -- --Database:Provider=Postgres "--ConnectionStrings:Default=Host=localhost;Database=novaforge;Username=postgres;Password=unused"
   ```
   (The connection string is only used to pick the PostgreSQL provider; nothing connects.)
   Commit the `Data/Migrations` folder. On startup the app applies them automatically
   on PostgreSQL. Local SQLite dev keeps creating its schema directly — delete `app_data/novaforge.db` after model changes.
3. Replace the template text on `/privacy` and `/terms` with reviewed legal text.

## 2. Option A — any Docker host (Hetzner, DigitalOcean, a VPS)

```bash
git clone https://github.com/MurtazaKabultrip/Tech-website.git && cd Tech-website
cp .env.example .env && nano .env        # real passwords and keys
docker compose up -d --build
```

Put a TLS-terminating reverse proxy in front (Caddy is simplest):

```
yourstudio.dk {
    reverse_proxy localhost:8080
}
```

The app trusts `X-Forwarded-Proto`, so HTTPS redirects and secure cookies work behind the proxy.

## 3. Option B — Azure App Service (Linux, .NET 10)

1. Create an App Service (Linux, .NET 10) and an Azure Database for PostgreSQL Flexible Server.
2. App settings: `Database__Provider=Postgres`, `ConnectionStrings__Default=...`, `Site__BaseUrl=https://...`,
   `Admin__Email`, `Admin__Password`, `Stripe__*`, `Ai__ApiKey`, `Smtp__*`.
3. Turn on **WebSockets** (required by the Blazor portal) and **ARR affinity** if you scale out.
4. Health check path: `/health`.
5. Deploy from GitHub Actions (`azure/webapps-deploy`) or `dotnet publish` + zip deploy.

## 4. Stripe

1. Dashboard → Developers → API keys: copy the secret key into `Stripe:SecretKey`.
2. Developers → Webhooks → Add endpoint `https://yourdomain/api/webhooks/stripe` with events
   `checkout.session.completed` and `checkout.session.async_payment_succeeded`. Copy the signing secret into
   `Stripe:WebhookSecret`.
3. Local testing: `stripe listen --forward-to https://localhost:7180/api/webhooks/stripe`.
4. Use test keys until you've paid a test invoice end-to-end, then switch to live keys.

## 5. Backups and monitoring

- PostgreSQL: daily `pg_dump` to off-site storage (or the managed service's point-in-time restore). Test a restore monthly.
- Keep the `app-keys` volume — losing it signs everyone out.
- Monitor `/health` with an uptime checker; ship logs to your host's log service. OpenTelemetry can be added
  with `OpenTelemetry.Extensions.Hosting` when you need traces and metrics.

## 6. Release checklist

- [ ] CI green on `main`
- [ ] Migrations reviewed
- [ ] Secrets set in the host, not in git
- [ ] `Site:BaseUrl` is the real HTTPS domain
- [ ] Stripe webhook delivers successfully (Stripe dashboard shows 200)
- [ ] Sign up, create a request, send a message, pay a test invoice

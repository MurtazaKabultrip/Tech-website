using Novaforge.Web.Domain;

namespace Novaforge.Web.Content;

public sealed record ServiceInfo(
    string Slug,
    string Title,
    string Summary,
    string Icon,
    string Intro,
    IReadOnlyList<string> Deliverables,
    IReadOnlyList<string> Stack,
    ProjectType ProjectType);

/// <summary>Single source of truth for the services shown on the site, the sitemap and the quote form.</summary>
public static class ServiceCatalog
{
    public static IReadOnlyList<ServiceInfo> All { get; } =
    [
        new("websites", "Website development",
            "Fast, responsive websites, web apps, dashboards, shops and SaaS products.",
            "globe",
            "From a sharp company site to a multi-tenant SaaS platform, we engineer for speed, search visibility and growth. Every build is responsive, accessible and measured against Core Web Vitals before launch.",
            ["Marketing sites & landing pages", "Web applications & dashboards", "E-commerce platforms", "SaaS products with subscriptions", "CMS and content workflows", "SEO-ready structure and metadata"],
            [".NET 10", "Blazor", "ASP.NET Core", "TypeScript", "PostgreSQL"],
            ProjectType.WebApp),

        new("ui-ux-design", "Futuristic UI/UX",
            "Premium interfaces with glass, motion, great typography and dark/light themes.",
            "sparkles",
            "We design interfaces that feel effortless: clear hierarchy, considered motion and accessible contrast in both light and dark themes — on every screen size.",
            ["Design systems & component libraries", "Interactive prototypes", "Micro-interactions & animation", "Dark / light theming", "Accessibility (WCAG 2.2 AA)", "Mobile-first layouts"],
            ["Figma", "CSS", "Blazor components", "Motion design"],
            ProjectType.Website),

        new("bug-fixing", "Bug fixing & rescue",
            "We diagnose and fix errors, broken features, security holes and slow pages.",
            "wrench",
            "Inherited a site that breaks, crashes or crawls? We reproduce the problem, find the root cause, fix it with tests so it stays fixed, and leave you a written report.",
            ["Error & crash diagnosis", "Broken feature repair", "Security vulnerability patching", "Performance profiling & fixes", "Regression tests for every fix", "Written root-cause report"],
            ["Any stack", "Profilers", "Static analysis", "Automated tests"],
            ProjectType.BugFix),

        new("upgrades", "Upgrades & modernisation",
            "Move outdated code to modern, supported, secure technology — safely.",
            "arrow-up",
            "Old frameworks stop getting security patches. We upgrade in safe, tested steps — for example .NET Framework to .NET 10 — without stopping your business.",
            [".NET Framework → .NET 10 migrations", "Dependency & framework upgrades", "Database migrations", "Monolith-to-modular refactoring", "Cloud migration", "Zero-downtime cut-over plans"],
            [".NET 10", "EF Core", "Docker", "CI/CD"],
            ProjectType.Upgrade),

        new("api-integrations", "API integrations",
            "REST, GraphQL, webhooks, AI services and third-party platforms connected reliably.",
            "plug",
            "We connect your product to the services it depends on — with retries, idempotency, monitoring and clear error handling, so integrations keep working when partners hiccup.",
            ["REST & GraphQL APIs", "Webhooks (inbound & outbound)", "CRM, ERP & marketing tools", "Booking, shipping & logistics APIs", "Custom public APIs with docs", "Monitoring & alerting"],
            ["ASP.NET Core", "HttpClient resilience", "OpenAPI", "Background jobs"],
            ProjectType.ApiIntegration),

        new("payments", "Payment gateways",
            "Secure checkout, subscriptions, recurring billing, invoices and refunds.",
            "card",
            "We implement payments the safe way: hosted checkouts keep card data off your servers, webhooks are signature-verified and idempotent, and every refund is audited.",
            ["One-off checkout", "Subscriptions & recurring billing", "Invoices & receipts", "Refunds & disputes handling", "Multi-currency (EUR, DKK, USD…)", "PCI-conscious architecture"],
            ["Stripe", "PayPal", "Adyen", "Signed webhooks"],
            ProjectType.PaymentIntegration),

        new("mobile-apps", "iOS & Android apps",
            "Cross-platform apps with login, push notifications, payments and store release.",
            "phone",
            "One C# codebase, native apps on both stores. We build with .NET MAUI against the same API as your website, so features ship everywhere at once.",
            ["iOS & Android from one codebase", "Secure login & biometrics", "Push notifications", "In-app payments", "Offline support", "App Store & Google Play release"],
            [".NET MAUI", "C#", "Push (APNs/FCM)", "REST API"],
            ProjectType.MobileApp),

        new("backend", "Backend & databases",
            "Scalable architecture, schemas, auth, background jobs and solid business logic.",
            "server",
            "The part users never see decides whether a product scales. We design clean data models, robust authorization and background processing that just works.",
            ["Architecture & database design", "Authentication & roles", "Background jobs & queues", "Caching & performance", "Audit logging", "Data import / export"],
            ["ASP.NET Core", "EF Core", "PostgreSQL", "SQL Server", "Redis"],
            ProjectType.WebApp),

        new("ai-automation", "AI & automation",
            "AI assistants, smart search and workflow automation where they genuinely help.",
            "cpu",
            "We add AI where it saves real time: assistants grounded in your data, semantic search, document processing and automated workflows — with guardrails and cost limits.",
            ["AI assistants & chat", "Semantic / intelligent search", "Document & email processing", "Workflow automation", "Prompt-injection safeguards", "Usage & cost monitoring"],
            ["Claude API", "Vector search", ".NET 10", "Background jobs"],
            ProjectType.AiAutomation),

        new("security", "Security",
            "Data protection, encryption, access control and vulnerability prevention.",
            "shield",
            "Security is built in, not bolted on: least-privilege access, encrypted data, hardened headers, dependency scanning and GDPR-conscious data handling.",
            ["Security audits", "OWASP Top 10 hardening", "Authentication & MFA", "Encryption at rest & in transit", "GDPR-conscious data handling", "Dependency vulnerability scanning"],
            ["ASP.NET Core Identity", "Data Protection", "CSP / HSTS", "Dependabot"],
            ProjectType.BugFix),

        new("testing", "Testing & performance",
            "Automated tests, load checks and performance tuning before every release.",
            "gauge",
            "We test, fix and retest before anything is called done. Automated suites run on every change, so quality doesn't depend on memory.",
            ["Unit & integration tests", "End-to-end browser tests", "Load & stress testing", "Core Web Vitals tuning", "Database query optimisation", "Release checklists"],
            ["xUnit", "Playwright", "k6", "dotnet-counters"],
            ProjectType.BugFix),

        new("devops", "Deployment & DevOps",
            "Cloud hosting, Docker, CI/CD, monitoring, backups and production deployments.",
            "cloud",
            "Push to main, ship to production — safely. We set up containerised deployments, automated pipelines, monitoring, alerts and tested backups.",
            ["Docker containers", "CI/CD pipelines", "Cloud hosting (Azure, AWS, Hetzner…)", "Monitoring & alerting", "Automated, tested backups", "Infrastructure as code"],
            ["Docker", "GitHub Actions", "Azure", "OpenTelemetry"],
            ProjectType.DevOps),
    ];

    public static ServiceInfo? Find(string slug) =>
        All.FirstOrDefault(s => string.Equals(s.Slug, slug, StringComparison.OrdinalIgnoreCase));
}

namespace Novaforge.Web.Services;

public sealed class SiteOptions
{
    public const string Section = "Site";

    public string Name { get; set; } = "Novaforge";
    public string Tagline { get; set; } = "Websites, apps and platforms engineered for what's next.";
    /// <summary>Public base URL, used for SEO (canonical, sitemap) and Stripe redirect URLs.</summary>
    public string BaseUrl { get; set; } = "https://localhost:5001";
    public string ContactEmail { get; set; } = "hello@example.com";
    /// <summary>Where new-lead and new-project notifications are sent.</summary>
    public string NotificationEmail { get; set; } = "hello@example.com";

    // ---- Legal details shown on the privacy policy and terms of service ----
    /// <summary>Registered company name, e.g. "Novaforge ApS". Defaults to <see cref="Name"/>.</summary>
    public string? LegalName { get; set; }
    /// <summary>Registered address (optional).</summary>
    public string? Address { get; set; }
    /// <summary>Company registration number, e.g. a Danish CVR number (optional).</summary>
    public string? RegistrationNumber { get; set; }
    /// <summary>Country whose law governs the terms.</summary>
    public string Jurisdiction { get; set; } = "Denmark";
    /// <summary>The data protection authority visitors can complain to.</summary>
    public string DataAuthority { get; set; } = "Datatilsynet (the Danish Data Protection Agency), www.datatilsynet.dk";
    /// <summary>Date the legal texts were last updated (shown on the pages).</summary>
    public string LegalUpdated { get; set; } = "28 October 2026";
}

public sealed class StripeOptions
{
    public const string Section = "Stripe";

    public string? SecretKey { get; set; }
    public string? WebhookSecret { get; set; }
    public string ApiBase { get; set; } = "https://api.stripe.com";
    public string DefaultCurrency { get; set; } = "eur";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SecretKey);
}

public sealed class AiOptions
{
    public const string Section = "Ai";

    /// <summary>Anthropic API key. When empty, AI features are hidden.</summary>
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "claude-sonnet-5-5";
    public string Endpoint { get; set; } = "https://api.anthropic.com/v1/messages";
    public int MaxTokens { get; set; } = 1500;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}

public sealed class SmtpOptions
{
    public const string Section = "Smtp";

    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string From { get; set; } = "no-reply@example.com";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);
}

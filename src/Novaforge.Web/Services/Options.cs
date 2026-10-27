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

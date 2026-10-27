using System.Globalization;

namespace Novaforge.Web.Domain;

/// <summary>Human-readable labels for enums and money, shared by the UI and e-mails.</summary>
public static class DisplayText
{
    public static string Of(ProjectType type) => type switch
    {
        ProjectType.Website => "Website",
        ProjectType.WebApp => "Web application",
        ProjectType.ECommerce => "E-commerce",
        ProjectType.SaaS => "SaaS product",
        ProjectType.MobileApp => "iOS / Android app",
        ProjectType.BugFix => "Bug fix",
        ProjectType.Upgrade => "Upgrade / modernisation",
        ProjectType.ApiIntegration => "API integration",
        ProjectType.PaymentIntegration => "Payment integration",
        ProjectType.AiAutomation => "AI & automation",
        ProjectType.DevOps => "Hosting & DevOps",
        _ => "Other"
    };

    public static string Of(ProjectStatus status) => status switch
    {
        ProjectStatus.Submitted => "Submitted",
        ProjectStatus.InReview => "In review",
        ProjectStatus.Quoted => "Quoted",
        ProjectStatus.InProgress => "In progress",
        ProjectStatus.Testing => "Testing",
        ProjectStatus.Delivered => "Delivered",
        ProjectStatus.Completed => "Completed",
        ProjectStatus.Cancelled => "Cancelled",
        _ => status.ToString()
    };

    public static string Of(BudgetRange budget) => budget switch
    {
        BudgetRange.Under2k => "Under €2,000",
        BudgetRange.From2kTo5k => "€2,000 – €5,000",
        BudgetRange.From5kTo15k => "€5,000 – €15,000",
        BudgetRange.From15kTo50k => "€15,000 – €50,000",
        BudgetRange.Over50k => "Over €50,000",
        _ => "Not sure yet"
    };

    public static string Of(InvoiceStatus status) => status switch
    {
        InvoiceStatus.Draft => "Draft",
        InvoiceStatus.Open => "Open",
        InvoiceStatus.Paid => "Paid",
        InvoiceStatus.Refunded => "Refunded",
        InvoiceStatus.Void => "Void",
        _ => status.ToString()
    };

    /// <summary>Formats minor units (cents) for display, e.g. 123450 + "eur" → "€1,234.50".</summary>
    public static string Money(long minor, string currency)
    {
        var amount = minor / 100m;
        var code = currency.ToUpperInvariant();
        var symbol = code switch
        {
            "EUR" => "€",
            "USD" => "$",
            "GBP" => "£",
            _ => null
        };
        var number = amount.ToString("N2", CultureInfo.InvariantCulture);
        return symbol is null ? $"{number} {code}" : $"{symbol}{number}";
    }

    public static string StatusCss(ProjectStatus status) => status switch
    {
        ProjectStatus.Completed or ProjectStatus.Delivered => "badge--ok",
        ProjectStatus.Cancelled => "badge--muted",
        ProjectStatus.InProgress or ProjectStatus.Testing => "badge--live",
        _ => "badge--info"
    };

    public static string StatusCss(InvoiceStatus status) => status switch
    {
        InvoiceStatus.Paid => "badge--ok",
        InvoiceStatus.Open => "badge--warn",
        _ => "badge--muted"
    };
}

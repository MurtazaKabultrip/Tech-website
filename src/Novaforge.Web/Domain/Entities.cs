using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Novaforge.Web.Domain;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Client = "Client";
}

public sealed class AppUser : IdentityUser
{
    [MaxLength(120)]
    public string FullName { get; set; } = "";

    [MaxLength(160)]
    public string? Company { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum ProjectType
{
    Website,
    WebApp,
    ECommerce,
    SaaS,
    MobileApp,
    BugFix,
    Upgrade,
    ApiIntegration,
    PaymentIntegration,
    AiAutomation,
    DevOps,
    Other
}

public enum ProjectStatus
{
    Submitted,
    InReview,
    Quoted,
    InProgress,
    Testing,
    Delivered,
    Completed,
    Cancelled
}

public enum BudgetRange
{
    NotSure,
    Under2k,
    From2kTo5k,
    From5kTo15k,
    From15kTo50k,
    Over50k
}

public sealed class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(450)]
    public string OwnerId { get; set; } = "";
    public AppUser? Owner { get; set; }

    [MaxLength(160)]
    public string Title { get; set; } = "";

    public ProjectType Type { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Submitted;
    public BudgetRange Budget { get; set; }

    [MaxLength(8000)]
    public string Description { get; set; } = "";

    /// <summary>Live URL of an existing site (bug fixes / upgrades).</summary>
    [MaxLength(500)]
    public string? ExistingUrl { get; set; }

    [MaxLength(500)]
    public string? RepositoryUrl { get; set; }

    /// <summary>Structured brief produced by the AI assistant (optional).</summary>
    [MaxLength(12000)]
    public string? AiBrief { get; set; }

    public DateTime? Deadline { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<ProjectMessage> Messages { get; set; } = [];
    public List<ProjectEvent> Events { get; set; } = [];
    public List<Invoice> Invoices { get; set; } = [];
}

public sealed class ProjectMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }

    [MaxLength(450)]
    public string AuthorId { get; set; } = "";

    [MaxLength(120)]
    public string AuthorName { get; set; } = "";

    public bool IsFromStaff { get; set; }

    [MaxLength(4000)]
    public string Body { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class ProjectEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }

    [MaxLength(300)]
    public string Text { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum LeadStatus
{
    New,
    Contacted,
    Converted,
    Archived
}

public sealed class Lead
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(120)]
    public string Name { get; set; } = "";

    [MaxLength(254)]
    public string Email { get; set; } = "";

    [MaxLength(160)]
    public string? Company { get; set; }

    public ProjectType ProjectType { get; set; }
    public BudgetRange Budget { get; set; }

    [MaxLength(4000)]
    public string Message { get; set; } = "";

    public LeadStatus Status { get; set; } = LeadStatus.New;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum InvoiceStatus
{
    Draft,
    Open,
    Paid,
    Refunded,
    Void
}

public sealed class Invoice
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(32)]
    public string Number { get; set; } = "";

    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }

    [MaxLength(450)]
    public string ClientId { get; set; } = "";

    /// <summary>ISO 4217, lower case (Stripe convention), e.g. "eur", "dkk".</summary>
    [MaxLength(3)]
    public string Currency { get; set; } = "eur";

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Open;
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime DueDate { get; set; } = DateTime.UtcNow.AddDays(14);
    public DateTime? PaidAt { get; set; }
    public DateTime? ReminderSentAt { get; set; }

    [MaxLength(255)]
    public string? StripeCheckoutSessionId { get; set; }

    [MaxLength(255)]
    public string? StripePaymentIntentId { get; set; }

    public long RefundedAmountMinor { get; set; }

    public List<InvoiceLine> Lines { get; set; } = [];

    /// <summary>Total in minor units (cents/øre). Money is never stored as floating point.</summary>
    public long TotalMinor => Lines.Sum(l => l.UnitAmountMinor * l.Quantity);

    public bool IsOverdue(DateTime nowUtc) => Status == InvoiceStatus.Open && DueDate < nowUtc;
}

public sealed class InvoiceLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InvoiceId { get; set; }

    [MaxLength(300)]
    public string Description { get; set; } = "";

    public int Quantity { get; set; } = 1;
    public long UnitAmountMinor { get; set; }
}

/// <summary>Stores processed Stripe event ids so webhooks are idempotent.</summary>
public sealed class ProcessedWebhookEvent
{
    [MaxLength(255)]
    public string Id { get; set; } = "";
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}

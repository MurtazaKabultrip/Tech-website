using System.ComponentModel.DataAnnotations;
using Novaforge.Web.Domain;

namespace Novaforge.Web.Services;

/// <summary>Like [Url] but treats empty strings as "not provided"; only http(s) is accepted.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class OptionalUrlAttribute : ValidationAttribute
{
    public OptionalUrlAttribute() : base("Enter a full URL starting with https://") { }

    public override bool IsValid(object? value) =>
        value is not string s || string.IsNullOrWhiteSpace(s) ||
        (Uri.TryCreate(s.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp));
}

// ---- Requests (shared by the Blazor UI and the JSON API) ----

public sealed class CreateProjectRequest
{
    [Required, StringLength(160, MinimumLength = 3)]
    public string Title { get; set; } = "";

    [Required]
    public ProjectType Type { get; set; } = ProjectType.Website;

    public BudgetRange Budget { get; set; } = BudgetRange.NotSure;

    [Required, StringLength(8000, MinimumLength = 20, ErrorMessage = "Please describe the project in at least 20 characters.")]
    public string Description { get; set; } = "";

    [OptionalUrl, StringLength(500)]
    public string? ExistingUrl { get; set; }

    [OptionalUrl, StringLength(500)]
    public string? RepositoryUrl { get; set; }

    public DateTime? Deadline { get; set; }

    [StringLength(12000)]
    public string? AiBrief { get; set; }
}

public sealed class AddMessageRequest
{
    [Required, StringLength(4000, MinimumLength = 1)]
    public string Body { get; set; } = "";
}

public sealed class UpdateStatusRequest
{
    [Required]
    public ProjectStatus Status { get; set; }

    [StringLength(300)]
    public string? Note { get; set; }
}

public sealed class CreateLeadRequest
{
    [Required, StringLength(120, MinimumLength = 2)]
    public string Name { get; set; } = "";

    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = "";

    [StringLength(160)]
    public string? Company { get; set; }

    public ProjectType ProjectType { get; set; } = ProjectType.Website;
    public BudgetRange Budget { get; set; } = BudgetRange.NotSure;

    [Required, StringLength(4000, MinimumLength = 10, ErrorMessage = "Tell us a little more (at least 10 characters).")]
    public string Message { get; set; } = "";

    /// <summary>Honeypot field: real users never fill it in.</summary>
    public string? Website { get; set; }
}

public sealed class InvoiceLineRequest
{
    [Required, StringLength(300, MinimumLength = 2)]
    public string Description { get; set; } = "";

    [Range(1, 10_000)]
    public int Quantity { get; set; } = 1;

    /// <summary>Unit price in minor units (cents). 150000 = €1,500.00.</summary>
    [Range(1, 100_000_000)]
    public long UnitAmountMinor { get; set; }
}

public sealed class CreateInvoiceRequest
{
    [Required]
    public Guid ProjectId { get; set; }

    [StringLength(3, MinimumLength = 3)]
    public string? Currency { get; set; }

    [Range(0, 120)]
    public int DueInDays { get; set; } = 14;

    [MinLength(1, ErrorMessage = "Add at least one line.")]
    public List<InvoiceLineRequest> Lines { get; set; } = [];
}

public sealed class RefundRequest
{
    /// <summary>Partial refund in minor units; null refunds everything remaining.</summary>
    [Range(1, 100_000_000)]
    public long? AmountMinor { get; set; }
}

public sealed class BriefRequest
{
    public ProjectType Type { get; set; } = ProjectType.Website;

    [Required, StringLength(4000, MinimumLength = 20)]
    public string Description { get; set; } = "";
}

// ---- Responses ----

public sealed record ProjectSummaryDto(
    Guid Id, string Title, ProjectType Type, ProjectStatus Status, BudgetRange Budget,
    string OwnerName, DateTime CreatedAt, DateTime UpdatedAt);

public sealed record MessageDto(Guid Id, string AuthorName, bool IsFromStaff, string Body, DateTime CreatedAt);

public sealed record EventDto(string Text, DateTime CreatedAt);

public sealed record ProjectDetailDto(
    Guid Id, string Title, ProjectType Type, ProjectStatus Status, BudgetRange Budget,
    string Description, string? ExistingUrl, string? RepositoryUrl, string? AiBrief, DateTime? Deadline,
    string OwnerId, string OwnerName, string? OwnerEmail, DateTime CreatedAt, DateTime UpdatedAt,
    IReadOnlyList<MessageDto> Messages, IReadOnlyList<EventDto> Events, IReadOnlyList<InvoiceDto> Invoices);

public sealed record InvoiceLineDto(string Description, int Quantity, long UnitAmountMinor);

public sealed record InvoiceDto(
    Guid Id, string Number, Guid ProjectId, string ProjectTitle, string Currency, InvoiceStatus Status,
    DateTime IssuedAt, DateTime DueDate, DateTime? PaidAt, long TotalMinor, long RefundedAmountMinor,
    bool IsOverdue, IReadOnlyList<InvoiceLineDto> Lines);

public sealed record DashboardDto(
    int ActiveProjects, int CompletedProjects, int OpenInvoices, long OutstandingMinor, string Currency,
    IReadOnlyList<ProjectSummaryDto> RecentProjects, IReadOnlyList<InvoiceDto> OpenInvoiceList);

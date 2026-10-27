using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Novaforge.Web.Data;
using Novaforge.Web.Domain;

namespace Novaforge.Web.Services;

public sealed class InvoiceService(
    IDbContextFactory<AppDbContext> dbFactory,
    IPaymentGateway payments,
    ProjectNotifier notifier,
    INotificationSender notifications,
    IOptions<SiteOptions> site,
    IOptions<StripeOptions> stripe,
    TimeProvider clock,
    ILogger<InvoiceService> logger)
{
    public static InvoiceDto ToDto(Invoice i, string projectTitle, DateTime nowUtc) => new(
        i.Id, i.Number, i.ProjectId, projectTitle, i.Currency, i.Status, i.IssuedAt, i.DueDate, i.PaidAt,
        i.TotalMinor, i.RefundedAmountMinor, i.IsOverdue(nowUtc),
        i.Lines.Select(l => new InvoiceLineDto(l.Description, l.Quantity, l.UnitAmountMinor)).ToList());

    public async Task<IReadOnlyList<InvoiceDto>> ListAsync(CurrentUser user, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var invoices = await db.Invoices.AsNoTracking()
            .Include(i => i.Lines).Include(i => i.Project)
            .Where(i => user.IsAdmin || i.ClientId == user.Id)
            .OrderByDescending(i => i.IssuedAt)
            .ToListAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        return invoices.Select(i => ToDto(i, i.Project?.Title ?? "", now)).ToList();
    }

    public async Task<InvoiceDto> GetAsync(CurrentUser user, Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var invoice = await db.Invoices.AsNoTracking()
            .Include(i => i.Lines).Include(i => i.Project)
            .FirstOrDefaultAsync(i => i.Id == id, ct);
        if (invoice is null || (!user.IsAdmin && invoice.ClientId != user.Id))
        {
            throw new NotFoundException("Invoice not found.");
        }
        return ToDto(invoice, invoice.Project?.Title ?? "", clock.GetUtcNow().UtcDateTime);
    }

    public async Task<InvoiceDto> CreateAsync(CurrentUser user, CreateInvoiceRequest request, CancellationToken ct = default)
    {
        if (!user.IsAdmin)
        {
            throw new UnauthorizedAccessException();
        }
        RequestValidator.Validate(request);

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var project = await db.Projects.Include(p => p.Owner).FirstOrDefaultAsync(p => p.Id == request.ProjectId, ct)
                      ?? throw ValidationFailedException.Single(nameof(request.ProjectId), "Project not found.");

        var now = clock.GetUtcNow().UtcDateTime;
        var currency = (Text.NullIfBlank(request.Currency) ?? stripe.Value.DefaultCurrency).ToLowerInvariant();

        // Sequential, human-friendly numbers per year. The unique index guards against races; retry on clash.
        for (var attempt = 0; ; attempt++)
        {
            var prefix = $"INV-{now.Year}-";
            var count = await db.Invoices.CountAsync(i => i.Number.StartsWith(prefix), ct);
            var invoice = new Invoice
            {
                Number = $"{prefix}{count + 1 + attempt:D4}",
                ProjectId = project.Id,
                ClientId = project.OwnerId,
                Currency = currency,
                Status = InvoiceStatus.Open,
                IssuedAt = now,
                DueDate = now.Date.AddDays(request.DueInDays),
                Lines = request.Lines.Select(l => new InvoiceLine
                {
                    Description = l.Description.Trim(),
                    Quantity = l.Quantity,
                    UnitAmountMinor = l.UnitAmountMinor
                }).ToList()
            };
            db.Invoices.Add(invoice);
            db.ProjectEvents.Add(new ProjectEvent
            {
                ProjectId = project.Id,
                Text = $"Invoice {invoice.Number} issued ({DisplayText.Money(invoice.TotalMinor, currency)})",
                CreatedAt = now
            });
            project.UpdatedAt = now;

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException) when (attempt < 3)
            {
                db.ChangeTracker.Clear();
                project = await db.Projects.Include(p => p.Owner).FirstAsync(p => p.Id == request.ProjectId, ct);
                continue;
            }

            await notifier.NotifyAsync(project.Id);
            if (project.Owner?.Email is { } email)
            {
                await notifications.SendAsync(email, $"Invoice {invoice.Number} from {site.Value.Name}",
                    $"A new invoice of {DisplayText.Money(invoice.TotalMinor, currency)} is due {invoice.DueDate:yyyy-MM-dd}.\n\n" +
                    $"View and pay: {site.Value.BaseUrl}/portal/invoices/{invoice.Id}", ct);
            }
            return ToDto(invoice, project.Title, now);
        }
    }

    /// <summary>Creates a hosted checkout page for an open invoice and returns its URL.</summary>
    public async Task<string> StartCheckoutAsync(CurrentUser user, Guid invoiceId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var invoice = await db.Invoices.Include(i => i.Lines).Include(i => i.Project).ThenInclude(p => p!.Owner)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct);
        if (invoice is null || (!user.IsAdmin && invoice.ClientId != user.Id))
        {
            throw new NotFoundException("Invoice not found.");
        }
        if (invoice.Status != InvoiceStatus.Open)
        {
            throw ValidationFailedException.Single("Status", $"This invoice is {DisplayText.Of(invoice.Status).ToLowerInvariant()}.");
        }

        var baseUrl = site.Value.BaseUrl.TrimEnd('/');
        var session = await payments.CreateCheckoutAsync(new CheckoutRequest(
            InvoiceId: invoice.Id,
            InvoiceNumber: invoice.Number,
            Currency: invoice.Currency,
            CustomerEmail: invoice.Project?.Owner?.Email,
            Lines: invoice.Lines.Select(l => new CheckoutLine(l.Description, l.Quantity, l.UnitAmountMinor)).ToList(),
            SuccessUrl: $"{baseUrl}/portal/invoices/{invoice.Id}?paid=1",
            CancelUrl: $"{baseUrl}/portal/invoices/{invoice.Id}?cancelled=1"), ct);

        invoice.StripeCheckoutSessionId = session.SessionId;
        await db.SaveChangesAsync(ct);
        return session.Url;
    }

    /// <summary>Called from the verified payment webhook. Idempotent.</summary>
    public async Task MarkPaidAsync(Guid invoiceId, string? sessionId, string? paymentIntentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var invoice = await db.Invoices.Include(i => i.Lines).Include(i => i.Project).ThenInclude(p => p!.Owner)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct);
        if (invoice is null)
        {
            logger.LogWarning("Payment received for unknown invoice {InvoiceId}", invoiceId);
            return;
        }
        if (invoice.Status == InvoiceStatus.Paid)
        {
            return;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        invoice.Status = InvoiceStatus.Paid;
        invoice.PaidAt = now;
        invoice.StripeCheckoutSessionId = sessionId ?? invoice.StripeCheckoutSessionId;
        invoice.StripePaymentIntentId = paymentIntentId ?? invoice.StripePaymentIntentId;
        db.ProjectEvents.Add(new ProjectEvent
        {
            ProjectId = invoice.ProjectId,
            Text = $"Invoice {invoice.Number} paid — thank you!",
            CreatedAt = now
        });
        await db.SaveChangesAsync(ct);
        await notifier.NotifyAsync(invoice.ProjectId);

        await notifications.SendAsync(site.Value.NotificationEmail, $"Payment received: {invoice.Number}",
            $"{DisplayText.Money(invoice.TotalMinor, invoice.Currency)} for \"{invoice.Project?.Title}\".", ct);
        if (invoice.Project?.Owner?.Email is { } email)
        {
            await notifications.SendAsync(email, $"Receipt for {invoice.Number}",
                $"We received your payment of {DisplayText.Money(invoice.TotalMinor, invoice.Currency)}. Thank you!\n\n" +
                $"{site.Value.BaseUrl}/portal/invoices/{invoice.Id}", ct);
        }
    }

    public async Task<InvoiceDto> RefundAsync(CurrentUser user, Guid invoiceId, RefundRequest request, CancellationToken ct = default)
    {
        if (!user.IsAdmin)
        {
            throw new UnauthorizedAccessException();
        }
        RequestValidator.Validate(request);

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var invoice = await db.Invoices.Include(i => i.Lines).Include(i => i.Project)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct) ?? throw new NotFoundException("Invoice not found.");

        if (invoice.Status != InvoiceStatus.Paid || string.IsNullOrEmpty(invoice.StripePaymentIntentId))
        {
            throw ValidationFailedException.Single("Status", "Only paid card invoices can be refunded.");
        }

        var remaining = invoice.TotalMinor - invoice.RefundedAmountMinor;
        var amount = request.AmountMinor ?? remaining;
        if (amount <= 0 || amount > remaining)
        {
            throw ValidationFailedException.Single(nameof(request.AmountMinor),
                $"Amount must be between 1 and {remaining} (minor units).");
        }

        await payments.RefundAsync(invoice.StripePaymentIntentId, amount, $"refund-{invoice.Id}-{invoice.RefundedAmountMinor}", ct);

        var now = clock.GetUtcNow().UtcDateTime;
        invoice.RefundedAmountMinor += amount;
        if (invoice.RefundedAmountMinor >= invoice.TotalMinor)
        {
            invoice.Status = InvoiceStatus.Refunded;
        }
        db.ProjectEvents.Add(new ProjectEvent
        {
            ProjectId = invoice.ProjectId,
            Text = $"Refund of {DisplayText.Money(amount, invoice.Currency)} issued for {invoice.Number}",
            CreatedAt = now
        });
        await db.SaveChangesAsync(ct);
        await notifier.NotifyAsync(invoice.ProjectId);
        return ToDto(invoice, invoice.Project?.Title ?? "", now);
    }

    public async Task VoidAsync(CurrentUser user, Guid invoiceId, CancellationToken ct = default)
    {
        if (!user.IsAdmin)
        {
            throw new UnauthorizedAccessException();
        }
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId, ct)
                      ?? throw new NotFoundException("Invoice not found.");
        if (invoice.Status != InvoiceStatus.Open)
        {
            throw ValidationFailedException.Single("Status", "Only open invoices can be voided.");
        }
        invoice.Status = InvoiceStatus.Void;
        await db.SaveChangesAsync(ct);
        await notifier.NotifyAsync(invoice.ProjectId);
    }
}

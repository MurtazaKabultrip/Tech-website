using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Novaforge.Web.Data;
using Novaforge.Web.Domain;

namespace Novaforge.Web.Services;

/// <summary>Hourly job: sends one reminder per overdue invoice.</summary>
public sealed class OverdueInvoiceReminderJob(
    IDbContextFactory<AppDbContext> dbFactory,
    INotificationSender notifications,
    IOptions<SiteOptions> site,
    TimeProvider clock,
    ILogger<OverdueInvoiceReminderJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1), clock);
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Overdue invoice job failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var overdue = await db.Invoices
            .Include(i => i.Lines).Include(i => i.Project).ThenInclude(p => p!.Owner)
            .Where(i => i.Status == InvoiceStatus.Open && i.DueDate < now && i.ReminderSentAt == null)
            .Take(100)
            .ToListAsync(ct);

        foreach (var invoice in overdue)
        {
            if (invoice.Project?.Owner?.Email is { } email)
            {
                await notifications.SendAsync(email, $"Reminder: invoice {invoice.Number} is overdue",
                    $"Invoice {invoice.Number} ({DisplayText.Money(invoice.TotalMinor, invoice.Currency)}) was due {invoice.DueDate:yyyy-MM-dd}.\n\n" +
                    $"Pay online: {site.Value.BaseUrl}/portal/invoices/{invoice.Id}", ct);
            }
            invoice.ReminderSentAt = now;
        }
        await db.SaveChangesAsync(ct);
        return overdue.Count;
    }
}

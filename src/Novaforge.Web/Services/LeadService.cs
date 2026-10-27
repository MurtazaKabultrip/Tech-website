using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Novaforge.Web.Data;
using Novaforge.Web.Domain;

namespace Novaforge.Web.Services;

public sealed class LeadService(
    IDbContextFactory<AppDbContext> dbFactory,
    INotificationSender notifications,
    IOptions<SiteOptions> site,
    TimeProvider clock,
    ILogger<LeadService> logger)
{
    /// <summary>Stores a quote request from the public site. Returns false if it was dropped as spam.</summary>
    public async Task<bool> SubmitAsync(CreateLeadRequest request, CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(request.Website))
        {
            logger.LogInformation("Dropped lead from honeypot submission");
            return false; // pretend success to the bot
        }
        RequestValidator.Validate(request);

        var lead = new Lead
        {
            Name = request.Name.Trim(),
            Email = request.Email.Trim(),
            Company = Text.NullIfBlank(request.Company),
            ProjectType = request.ProjectType,
            Budget = request.Budget,
            Message = request.Message.Trim(),
            CreatedAt = clock.GetUtcNow().UtcDateTime
        };

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Leads.Add(lead);
        await db.SaveChangesAsync(ct);

        await notifications.SendAsync(site.Value.NotificationEmail,
            $"New enquiry: {DisplayText.Of(lead.ProjectType)} from {lead.Name}",
            $"{lead.Name} <{lead.Email}> {lead.Company}\nBudget: {DisplayText.Of(lead.Budget)}\n\n{lead.Message}", ct);
        await notifications.SendAsync(lead.Email, $"Thanks for contacting {site.Value.Name}",
            $"Hi {lead.Name},\n\nThanks for your message — we reply within one business day.\n\n— {site.Value.Name}", ct);
        return true;
    }

    public async Task<IReadOnlyList<Lead>> ListAsync(CurrentUser user, CancellationToken ct = default)
    {
        if (!user.IsAdmin)
        {
            throw new UnauthorizedAccessException();
        }
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Leads.AsNoTracking().OrderByDescending(l => l.CreatedAt).Take(500).ToListAsync(ct);
    }

    public async Task SetStatusAsync(CurrentUser user, Guid id, LeadStatus status, CancellationToken ct = default)
    {
        if (!user.IsAdmin)
        {
            throw new UnauthorizedAccessException();
        }
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var lead = await db.Leads.FirstOrDefaultAsync(l => l.Id == id, ct) ?? throw new NotFoundException("Lead not found.");
        lead.Status = status;
        await db.SaveChangesAsync(ct);
    }
}

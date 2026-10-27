using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Novaforge.Web.Data;
using Novaforge.Web.Domain;

namespace Novaforge.Web.Services;

public sealed class ProjectService(
    IDbContextFactory<AppDbContext> dbFactory,
    ProjectNotifier notifier,
    INotificationSender notifications,
    IOptions<SiteOptions> site,
    TimeProvider clock)
{
    public async Task<IReadOnlyList<ProjectSummaryDto>> ListAsync(CurrentUser user, ProjectStatus? status = null, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var query = db.Projects.AsNoTracking().AsQueryable();
        if (!user.IsAdmin)
        {
            query = query.Where(p => p.OwnerId == user.Id);
        }
        if (status is not null)
        {
            query = query.Where(p => p.Status == status);
        }

        return await query
            .OrderByDescending(p => p.UpdatedAt)
            .Select(p => new ProjectSummaryDto(p.Id, p.Title, p.Type, p.Status, p.Budget,
                p.Owner != null ? p.Owner.FullName : "", p.CreatedAt, p.UpdatedAt))
            .ToListAsync(ct);
    }

    public async Task<ProjectDetailDto> GetAsync(CurrentUser user, Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var project = await db.Projects.AsNoTracking()
            .Include(p => p.Owner)
            .Include(p => p.Messages)
            .Include(p => p.Events)
            .Include(p => p.Invoices).ThenInclude(i => i.Lines)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        // Same response for "missing" and "not yours" so ids can't be probed.
        if (project is null || (!user.IsAdmin && project.OwnerId != user.Id))
        {
            throw new NotFoundException("Project not found.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        return new ProjectDetailDto(
            project.Id, project.Title, project.Type, project.Status, project.Budget, project.Description,
            project.ExistingUrl, project.RepositoryUrl, project.AiBrief, project.Deadline,
            project.OwnerId, project.Owner?.FullName ?? "", project.Owner?.Email,
            project.CreatedAt, project.UpdatedAt,
            project.Messages.OrderBy(m => m.CreatedAt)
                .Select(m => new MessageDto(m.Id, m.AuthorName, m.IsFromStaff, m.Body, m.CreatedAt)).ToList(),
            project.Events.OrderByDescending(e => e.CreatedAt)
                .Select(e => new EventDto(e.Text, e.CreatedAt)).ToList(),
            project.Invoices.OrderByDescending(i => i.IssuedAt)
                .Select(i => InvoiceService.ToDto(i, project.Title, now)).ToList());
    }

    public async Task<Guid> CreateAsync(CurrentUser user, CreateProjectRequest request, CancellationToken ct = default)
    {
        RequestValidator.Validate(request);
        var now = clock.GetUtcNow().UtcDateTime;

        var project = new Project
        {
            OwnerId = user.Id,
            Title = request.Title.Trim(),
            Type = request.Type,
            Budget = request.Budget,
            Description = request.Description.Trim(),
            ExistingUrl = Text.NullIfBlank(request.ExistingUrl),
            RepositoryUrl = Text.NullIfBlank(request.RepositoryUrl),
            AiBrief = Text.NullIfBlank(request.AiBrief),
            Deadline = request.Deadline?.ToUniversalTime(),
            CreatedAt = now,
            UpdatedAt = now
        };
        project.Events.Add(new ProjectEvent { Text = $"{DisplayText.Of(project.Type)} request submitted", CreatedAt = now });

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);

        await notifications.SendAsync(site.Value.NotificationEmail,
            $"New project: {project.Title}",
            $"{user.Name} submitted a {DisplayText.Of(project.Type)} request.\n\n{project.Description}\n\n{site.Value.BaseUrl}/portal/projects/{project.Id}",
            ct);

        return project.Id;
    }

    public async Task<MessageDto> AddMessageAsync(CurrentUser user, Guid projectId, AddMessageRequest request, CancellationToken ct = default)
    {
        RequestValidator.Validate(request);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var project = await db.Projects.Include(p => p.Owner).FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null || (!user.IsAdmin && project.OwnerId != user.Id))
        {
            throw new NotFoundException("Project not found.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var message = new ProjectMessage
        {
            ProjectId = projectId,
            AuthorId = user.Id,
            AuthorName = user.Name,
            IsFromStaff = user.IsAdmin,
            Body = request.Body.Trim(),
            CreatedAt = now
        };
        db.ProjectMessages.Add(message);
        project.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        await notifier.NotifyAsync(projectId);

        // Tell the other side.
        var recipient = user.IsAdmin ? project.Owner?.Email : site.Value.NotificationEmail;
        if (!string.IsNullOrEmpty(recipient))
        {
            await notifications.SendAsync(recipient, $"New message on \"{project.Title}\"",
                $"{user.Name} wrote:\n\n{message.Body}\n\n{site.Value.BaseUrl}/portal/projects/{projectId}", ct);
        }

        return new MessageDto(message.Id, message.AuthorName, message.IsFromStaff, message.Body, message.CreatedAt);
    }

    public async Task UpdateStatusAsync(CurrentUser user, Guid projectId, UpdateStatusRequest request, CancellationToken ct = default)
    {
        if (!user.IsAdmin)
        {
            throw new UnauthorizedAccessException();
        }
        RequestValidator.Validate(request);

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var project = await db.Projects.Include(p => p.Owner).FirstOrDefaultAsync(p => p.Id == projectId, ct)
                      ?? throw new NotFoundException("Project not found.");
        if (project.Status == request.Status)
        {
            return;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        project.Status = request.Status;
        project.UpdatedAt = now;
        var text = $"Status changed to {DisplayText.Of(request.Status)}";
        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            text += $" — {request.Note.Trim()}";
        }
        db.ProjectEvents.Add(new ProjectEvent { ProjectId = projectId, Text = text, CreatedAt = now });
        await db.SaveChangesAsync(ct);
        await notifier.NotifyAsync(projectId);

        if (project.Owner?.Email is { } email)
        {
            await notifications.SendAsync(email, $"\"{project.Title}\" is now {DisplayText.Of(request.Status)}",
                $"{text}\n\n{site.Value.BaseUrl}/portal/projects/{projectId}", ct);
        }
    }

    public async Task<DashboardDto> GetDashboardAsync(CurrentUser user, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var projects = db.Projects.AsNoTracking().Where(p => user.IsAdmin || p.OwnerId == user.Id);

        var active = await projects.CountAsync(p => p.Status != ProjectStatus.Completed && p.Status != ProjectStatus.Cancelled, ct);
        var completed = await projects.CountAsync(p => p.Status == ProjectStatus.Completed, ct);

        var recent = await projects.OrderByDescending(p => p.UpdatedAt).Take(5)
            .Select(p => new ProjectSummaryDto(p.Id, p.Title, p.Type, p.Status, p.Budget,
                p.Owner != null ? p.Owner.FullName : "", p.CreatedAt, p.UpdatedAt))
            .ToListAsync(ct);

        var openInvoices = await db.Invoices.AsNoTracking()
            .Include(i => i.Lines).Include(i => i.Project)
            .Where(i => i.Status == InvoiceStatus.Open && (user.IsAdmin || i.ClientId == user.Id))
            .OrderBy(i => i.DueDate)
            .ToListAsync(ct);

        var now = clock.GetUtcNow().UtcDateTime;
        var dtos = openInvoices.Select(i => InvoiceService.ToDto(i, i.Project?.Title ?? "", now)).ToList();
        var currency = dtos.FirstOrDefault()?.Currency ?? "eur";
        var outstanding = dtos.Where(d => d.Currency == currency).Sum(d => d.TotalMinor);

        return new DashboardDto(active, completed, dtos.Count, outstanding, currency, recent, dtos);
    }
}

internal static class Text
{
    public static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

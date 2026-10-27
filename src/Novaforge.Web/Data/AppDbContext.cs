using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Novaforge.Web.Domain;

namespace Novaforge.Web.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMessage> ProjectMessages => Set<ProjectMessage>();
    public DbSet<ProjectEvent> ProjectEvents => Set<ProjectEvent>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<ProcessedWebhookEvent> ProcessedWebhookEvents => Set<ProcessedWebhookEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Project>(e =>
        {
            e.Property(p => p.Type).HasConversion<string>().HasMaxLength(32);
            e.Property(p => p.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(p => p.Budget).HasConversion<string>().HasMaxLength(32);
            e.HasOne(p => p.Owner).WithMany().HasForeignKey(p => p.OwnerId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(p => p.Messages).WithOne().HasForeignKey(m => m.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.Events).WithOne().HasForeignKey(m => m.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.Invoices).WithOne(i => i.Project).HasForeignKey(i => i.ProjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(p => new { p.OwnerId, p.UpdatedAt });
            e.HasIndex(p => p.Status);
        });

        builder.Entity<ProjectMessage>().HasIndex(m => new { m.ProjectId, m.CreatedAt });
        builder.Entity<ProjectEvent>().HasIndex(m => new { m.ProjectId, m.CreatedAt });

        builder.Entity<Lead>(e =>
        {
            e.Property(l => l.ProjectType).HasConversion<string>().HasMaxLength(32);
            e.Property(l => l.Budget).HasConversion<string>().HasMaxLength(32);
            e.Property(l => l.Status).HasConversion<string>().HasMaxLength(32);
            e.HasIndex(l => l.CreatedAt);
        });

        builder.Entity<Invoice>(e =>
        {
            e.Property(i => i.Status).HasConversion<string>().HasMaxLength(32);
            e.Ignore(i => i.TotalMinor);
            e.HasIndex(i => i.Number).IsUnique();
            e.HasIndex(i => i.ClientId);
            e.HasIndex(i => i.StripeCheckoutSessionId);
            e.HasMany(i => i.Lines).WithOne().HasForeignKey(l => l.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ProcessedWebhookEvent>().HasKey(e => e.Id);
    }
}

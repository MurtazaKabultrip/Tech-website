using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Novaforge.Web.Domain;

namespace Novaforge.Web.Data;

public static class DbInitializer
{
    /// <summary>
    /// PostgreSQL: applies migrations when the project has them (generated for PostgreSQL), otherwise creates the schema.
    /// SQLite (local dev, tests): always creates the schema directly. Then seeds roles and the first admin account.
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration config, ILogger logger)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (!db.Database.IsSqlite() && db.Database.GetMigrations().Any())
        {
            logger.LogInformation("Applying database migrations");
            await db.Database.MigrateAsync();
        }
        else
        {
            await db.Database.EnsureCreatedAsync();
        }

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { Roles.Admin, Roles.Client })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var adminEmail = config["Admin:Email"];
        var adminPassword = config["Admin:Password"];
        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            return;
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var admin = await userManager.FindByEmailAsync(adminEmail);
        if (admin is null)
        {
            admin = new AppUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FullName = config["Admin:FullName"] ?? "Administrator"
            };
            var created = await userManager.CreateAsync(admin, adminPassword);
            if (!created.Succeeded)
            {
                logger.LogError("Could not create admin account: {Errors}",
                    string.Join("; ", created.Errors.Select(e => e.Description)));
                return;
            }
            logger.LogInformation("Seeded admin account {Email}", adminEmail);
        }

        if (!await userManager.IsInRoleAsync(admin, Roles.Admin))
        {
            await userManager.AddToRoleAsync(admin, Roles.Admin);
        }
    }
}

using System.Text.Json;

namespace Novaforge.Web.Services;

/// <summary>Site-wide appearance settings, changed by the admin and applied to every visitor.</summary>
public sealed record SiteSettings(string Theme)
{
    public static readonly SiteSettings Default = new("dark");
    public bool IsDark => Theme != "light";
}

/// <summary>
/// Stores <see cref="SiteSettings"/> in a small JSON file (app_data/site-settings.json by default) so it survives
/// restarts without a database migration. Reads are cached in memory; writes are atomic.
/// </summary>
public sealed class SiteSettingsService
{
    private readonly string path;
    private readonly ILogger<SiteSettingsService> logger;
    private readonly object gate = new();
    private SiteSettings current;

    public SiteSettingsService(IConfiguration config, IHostEnvironment env, ILogger<SiteSettingsService> logger)
    {
        this.logger = logger;
        var configured = config["Site:SettingsPath"];
        path = Path.GetFullPath(string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(env.ContentRootPath, "app_data", "site-settings.json")
            : configured);
        current = Load();
    }

    public SiteSettings Current => current;

    public event Action<SiteSettings>? Changed;

    public void SetTheme(string theme)
    {
        if (theme is not ("dark" or "light"))
        {
            throw ValidationFailedException.Single(nameof(theme), "Theme must be dark or light.");
        }

        lock (gate)
        {
            current = current with { Theme = theme };
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(current));
            File.Move(tmp, path, overwrite: true);
        }
        logger.LogInformation("Site theme changed to {Theme}", theme);
        Changed?.Invoke(current);
    }

    private SiteSettings Load()
    {
        try
        {
            if (File.Exists(path))
            {
                var loaded = JsonSerializer.Deserialize<SiteSettings>(File.ReadAllText(path));
                if (loaded is { Theme: "dark" or "light" })
                {
                    return loaded;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not read site settings from {Path}; using defaults", path);
        }
        return SiteSettings.Default;
    }
}

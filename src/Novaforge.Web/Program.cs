using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Novaforge.Web.Components;
using Novaforge.Web.Data;
using Novaforge.Web.Domain;
using Novaforge.Web.Endpoints;
using Novaforge.Web.Services;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ---------- Options ----------
builder.Services.Configure<SiteOptions>(config.GetSection(SiteOptions.Section));
builder.Services.Configure<StripeOptions>(config.GetSection(StripeOptions.Section));
builder.Services.Configure<AiOptions>(config.GetSection(AiOptions.Section));
builder.Services.Configure<SmtpOptions>(config.GetSection(SmtpOptions.Section));
builder.Services.AddSingleton(TimeProvider.System);

// ---------- Database ----------
// "Sqlite" (zero-setup local dev) or "Postgres" (production / docker compose).
var provider = config["Database:Provider"] ?? "Sqlite";
var connectionString = config.GetConnectionString("Default") ?? "Data Source=app_data/novaforge.db";
if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
{
    var dataSource = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString).DataSource;
    var dir = Path.GetDirectoryName(Path.GetFullPath(dataSource));
    if (!string.IsNullOrEmpty(dir))
    {
        Directory.CreateDirectory(dir);
    }
}

void ConfigureDb(DbContextOptionsBuilder options)
{
    if (provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
    {
        options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure());
    }
    else
    {
        options.UseSqlite(connectionString);
    }
}

// Blazor components use the factory (one context per operation); Identity uses the scoped context.
builder.Services.AddDbContextFactory<AppDbContext>(ConfigureDb);
builder.Services.AddDbContext<AppDbContext>(ConfigureDb, optionsLifetime: ServiceLifetime.Singleton);

// ---------- Identity & auth ----------
// One user store serves both the website (cookie) and the mobile app / API (bearer token).
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddBearerToken(IdentityConstants.BearerScheme)
    .AddIdentityCookies();

builder.Services.AddIdentityCore<AppUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 10;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders()
    .AddApiEndpoints()
    .AddClaimsPrincipalFactory<AppClaimsFactory>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/account/login";
    options.AccessDeniedPath = "/account/access-denied";
    options.Cookie.Name = "nf.auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // HTTPS is enforced by redirect + HSTS
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    // API callers get status codes, not login-page redirects.
    options.Events.OnRedirectToLogin = ctx => ApiAwareRedirect(ctx.HttpContext, ctx.RedirectUri, StatusCodes.Status401Unauthorized);
    options.Events.OnRedirectToAccessDenied = ctx => ApiAwareRedirect(ctx.HttpContext, ctx.RedirectUri, StatusCodes.Status403Forbidden);
});

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(ApiEndpoints.ApiPolicy, p => p
        .AddAuthenticationSchemes(IdentityConstants.BearerScheme, IdentityConstants.ApplicationScheme)
        .RequireAuthenticatedUser())
    .AddPolicy(ApiEndpoints.AdminPolicy, p => p
        .AddAuthenticationSchemes(IdentityConstants.BearerScheme, IdentityConstants.ApplicationScheme)
        .RequireRole(Roles.Admin));

builder.Services.AddCascadingAuthenticationState();

// Keys must persist across restarts/instances or everyone gets logged out on deploy.
var keysPath = config["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(keysPath))
{
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keysPath)).SetApplicationName("Novaforge");
}

// ---------- App services ----------
builder.Services.AddSingleton<ProjectNotifier>();
builder.Services.AddSingleton<SiteSettingsService>();
builder.Services.AddSingleton<INotificationSender, EmailNotificationSender>();
builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<InvoiceService>();
builder.Services.AddScoped<LeadService>();
builder.Services.AddHttpClient<IPaymentGateway, StripePaymentGateway>(c => c.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddHttpClient<IAiAssistant, AnthropicAssistant>(c => c.Timeout = TimeSpan.FromSeconds(90));
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHostedService<OverdueInvoiceReminderJob>();
}

// ---------- Web ----------
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddResponseCompression(o => o.EnableForHttps = true);

var publicPerMinute = config.GetValue("RateLimits:PublicPerMinute", 5);
var authPerMinute = config.GetValue("RateLimits:AuthPerMinute", 20);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("public", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = publicPerMinute, Window = TimeSpan.FromMinutes(1) }));
    options.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = authPerMinute, Window = TimeSpan.FromMinutes(1) }));
    options.AddPolicy("ai", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.User.Identity?.Name ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(10) }));
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Behind a container platform / reverse proxy. Restrict KnownProxies/KnownNetworks if your host publishes them.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

await DbInitializer.InitializeAsync(app.Services, config, app.Logger);

// ---------- Pipeline ----------
app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", createScopeForErrors: true); // HTML error page for the website
    app.UseHsts();
}
// JSON API: service exceptions become RFC 7807 problem responses (ApiExceptionHandler).
app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/api"), api => api.UseExceptionHandler());

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.Use(async (context, next) =>
{
    // JSON API callers get bare status codes / problem details, never the HTML 404 page.
    if (context.Request.Path.StartsWithSegments("/api")
        && context.Features.Get<Microsoft.AspNetCore.Diagnostics.IStatusCodePagesFeature>() is { } feature)
    {
        feature.Enabled = false;
    }
    await next();
});
app.UseHttpsRedirection();
app.UseResponseCompression();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.MapAccountEndpoints();
app.MapSeoEndpoints();
app.MapApi();
app.MapHealthChecks("/health");
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // /openapi/v1.json
}

app.Run();

static Task ApiAwareRedirect(HttpContext context, string redirectUri, int apiStatus)
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.StatusCode = apiStatus;
    }
    else
    {
        context.Response.Redirect(redirectUri);
    }
    return Task.CompletedTask;
}

public partial class Program;

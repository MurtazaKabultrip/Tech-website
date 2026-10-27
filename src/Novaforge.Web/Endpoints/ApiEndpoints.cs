using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Novaforge.Web.Data;
using Novaforge.Web.Domain;
using Novaforge.Web.Services;

namespace Novaforge.Web.Endpoints;

/// <summary>
/// JSON API used by the future iOS/Android app and third parties. Accepts bearer tokens
/// (POST /api/auth/login) and the web auth cookie.
/// </summary>
public static class ApiEndpoints
{
    public const string ApiPolicy = "Api";
    public const string AdminPolicy = "ApiAdmin";

    public static IEndpointRouteBuilder MapApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").WithTags("API");

        // Identity endpoints: /api/auth/register, /login, /refresh, /manage/info, ...
        api.MapGroup("/auth").WithTags("Auth").RequireRateLimiting("auth").MapIdentityApi<AppUser>();

        api.MapGet("/me", (ClaimsPrincipal principal) =>
        {
            var user = CurrentUser.From(principal)!;
            return TypedResults.Ok(new { user.Id, user.Name, user.IsAdmin, Email = principal.FindFirstValue(ClaimTypes.Email) });
        }).RequireAuthorization(ApiPolicy);

        // Public quote request (website form, other sites, mobile).
        api.MapPost("/leads", async ([FromBody] CreateLeadRequest request, LeadService leads, CancellationToken ct) =>
        {
            await leads.SubmitAsync(request, ct);
            return TypedResults.Accepted((string?)null);
        }).RequireRateLimiting("public").AllowAnonymous().WithTags("Leads");

        var projects = api.MapGroup("/projects").RequireAuthorization(ApiPolicy).WithTags("Projects");
        projects.MapGet("/", (ClaimsPrincipal p, ProjectService s, ProjectStatus? status, CancellationToken ct) =>
            s.ListAsync(User(p), status, ct));
        projects.MapGet("/{id:guid}", (Guid id, ClaimsPrincipal p, ProjectService s, CancellationToken ct) =>
            s.GetAsync(User(p), id, ct));
        projects.MapPost("/", async ([FromBody] CreateProjectRequest request, ClaimsPrincipal p, ProjectService s, CancellationToken ct) =>
        {
            var id = await s.CreateAsync(User(p), request, ct);
            return TypedResults.Created($"/api/projects/{id}", new { id });
        });
        projects.MapPost("/{id:guid}/messages", async (Guid id, [FromBody] AddMessageRequest request, ClaimsPrincipal p, ProjectService s, CancellationToken ct) =>
            TypedResults.Ok(await s.AddMessageAsync(User(p), id, request, ct)));
        projects.MapPut("/{id:guid}/status", async (Guid id, [FromBody] UpdateStatusRequest request, ClaimsPrincipal p, ProjectService s, CancellationToken ct) =>
        {
            await s.UpdateStatusAsync(User(p), id, request, ct);
            return TypedResults.NoContent();
        }).RequireAuthorization(AdminPolicy);

        api.MapGet("/dashboard", (ClaimsPrincipal p, ProjectService s, CancellationToken ct) => s.GetDashboardAsync(User(p), ct))
            .RequireAuthorization(ApiPolicy).WithTags("Projects");

        var invoices = api.MapGroup("/invoices").RequireAuthorization(ApiPolicy).WithTags("Invoices");
        invoices.MapGet("/", (ClaimsPrincipal p, InvoiceService s, CancellationToken ct) => s.ListAsync(User(p), ct));
        invoices.MapGet("/{id:guid}", (Guid id, ClaimsPrincipal p, InvoiceService s, CancellationToken ct) => s.GetAsync(User(p), id, ct));
        invoices.MapPost("/", async ([FromBody] CreateInvoiceRequest request, ClaimsPrincipal p, InvoiceService s, CancellationToken ct) =>
        {
            var invoice = await s.CreateAsync(User(p), request, ct);
            return TypedResults.Created($"/api/invoices/{invoice.Id}", invoice);
        }).RequireAuthorization(AdminPolicy);
        invoices.MapPost("/{id:guid}/checkout", async (Guid id, ClaimsPrincipal p, InvoiceService s, CancellationToken ct) =>
            TypedResults.Ok(new { url = await s.StartCheckoutAsync(User(p), id, ct) }));
        invoices.MapPost("/{id:guid}/refund", async (Guid id, [FromBody] RefundRequest request, ClaimsPrincipal p, InvoiceService s, CancellationToken ct) =>
            TypedResults.Ok(await s.RefundAsync(User(p), id, request, ct))).RequireAuthorization(AdminPolicy);
        invoices.MapPost("/{id:guid}/void", async (Guid id, ClaimsPrincipal p, InvoiceService s, CancellationToken ct) =>
        {
            await s.VoidAsync(User(p), id, ct);
            return TypedResults.NoContent();
        }).RequireAuthorization(AdminPolicy);

        api.MapGet("/admin/leads", (ClaimsPrincipal p, LeadService s, CancellationToken ct) => s.ListAsync(User(p), ct))
            .RequireAuthorization(AdminPolicy).WithTags("Leads");

        api.MapPost("/ai/brief", async ([FromBody] BriefRequest request, IAiAssistant ai, CancellationToken ct) =>
        {
            RequestValidator.Validate(request);
            return TypedResults.Ok(new { brief = await ai.GenerateBriefAsync(request.Type, request.Description, ct) });
        }).RequireAuthorization(ApiPolicy).RequireRateLimiting("ai").WithTags("AI");

        api.MapPost("/webhooks/stripe", HandleStripeWebhookAsync).AllowAnonymous().WithTags("Webhooks")
            .ExcludeFromDescription();

        return app;
    }

    private static CurrentUser User(ClaimsPrincipal principal) =>
        CurrentUser.From(principal) ?? throw new UnauthorizedAccessException();

    internal static async Task<IResult> HandleStripeWebhookAsync(
        HttpRequest request,
        IOptions<StripeOptions> stripe,
        IDbContextFactory<AppDbContext> dbFactory,
        InvoiceService invoices,
        TimeProvider clock,
        ILogger<StripeOptions> logger,
        CancellationToken ct)
    {
        using var reader = new StreamReader(request.Body);
        var payload = await reader.ReadToEndAsync(ct);

        if (!StripeWebhookVerifier.IsValid(payload, request.Headers["Stripe-Signature"], stripe.Value.WebhookSecret ?? "", clock.GetUtcNow()))
        {
            logger.LogWarning("Rejected Stripe webhook with invalid signature");
            return TypedResults.BadRequest();
        }

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        var eventId = root.GetProperty("id").GetString() ?? "";
        var type = root.GetProperty("type").GetString();

        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            if (await db.ProcessedWebhookEvents.AnyAsync(e => e.Id == eventId, ct))
            {
                return TypedResults.Ok(); // already handled (Stripe retries)
            }
        }

        if (type is "checkout.session.completed" or "checkout.session.async_payment_succeeded")
        {
            var session = root.GetProperty("data").GetProperty("object");
            var paid = session.TryGetProperty("payment_status", out var ps) && ps.GetString() == "paid";
            if (paid
                && session.TryGetProperty("metadata", out var metadata)
                && metadata.TryGetProperty("invoice_id", out var idProp)
                && Guid.TryParse(idProp.GetString(), out var invoiceId))
            {
                var paymentIntent = session.TryGetProperty("payment_intent", out var pi) && pi.ValueKind == JsonValueKind.String
                    ? pi.GetString()
                    : null;
                await invoices.MarkPaidAsync(invoiceId, session.GetProperty("id").GetString(), paymentIntent, ct);
            }
        }

        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            db.ProcessedWebhookEvents.Add(new ProcessedWebhookEvent { Id = eventId, ProcessedAt = clock.GetUtcNow().UtcDateTime });
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Concurrent delivery of the same event; MarkPaidAsync is idempotent so this is safe.
            }
        }

        return TypedResults.Ok();
    }
}

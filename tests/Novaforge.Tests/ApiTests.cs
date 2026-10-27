using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Novaforge.Web.Domain;
using Novaforge.Web.Services;

namespace Novaforge.Tests;

[Collection(AppCollection.Name)]
public sealed class SiteTests(AppFactory app)
{
    [Fact]
    public async Task Health_endpoint_reports_healthy()
    {
        var response = await app.CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Home_page_renders_with_security_headers()
    {
        var response = await app.CreateClient().GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("that people remember.", html);
        Assert.DoesNotContain("/account/login", html); // no sign-in links for visitors
        Assert.Contains("<meta name=\"description\"", html);
        Assert.True(response.Headers.Contains("Content-Security-Policy"));
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
    }

    [Theory]
    [InlineData("/services")]
    [InlineData("/services/bug-fixing")]
    [InlineData("/process")]
    [InlineData("/contact")]
    [InlineData("/contact?type=BugFix")]
    [InlineData("/privacy")]
    [InlineData("/terms")]
    [InlineData("/account/login")]
    [InlineData("/account/register")]
    public async Task Public_pages_render(string path)
    {
        var response = await app.CreateClient().GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_service_returns_404()
    {
        var response = await app.CreateClient().GetAsync("/services/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Sitemap_and_robots_are_served()
    {
        var client = app.CreateClient();
        var sitemap = await client.GetStringAsync("/sitemap.xml");
        Assert.Contains("https://test.local/services/payments", sitemap);

        var robots = await client.GetStringAsync("/robots.txt");
        Assert.Contains("Disallow: /portal", robots);
        Assert.Contains("Sitemap: https://test.local/sitemap.xml", robots);
    }

    [Fact]
    public async Task Portal_redirects_anonymous_visitors_to_login()
    {
        var client = app.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/portal");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/account/login", response.Headers.Location!.ToString());
    }
}

[Collection(AppCollection.Name)]
public sealed class LeadApiTests(AppFactory app)
{
    [Fact]
    public async Task Invalid_lead_returns_validation_problem()
    {
        var response = await app.CreateClient().PostAsJsonAsync("/api/leads", new { name = "", email = "not-an-email", message = "hi" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.ReadAsync<JsonElement>();
        var errors = problem.GetProperty("errors");
        Assert.True(errors.TryGetProperty("Email", out _));
        Assert.True(errors.TryGetProperty("Name", out _));
        Assert.True(errors.TryGetProperty("Message", out _));
    }

    [Fact]
    public async Task Valid_lead_is_stored_and_visible_to_admin()
    {
        var email = $"lead-{Guid.NewGuid():N}@example.com";
        var response = await app.CreateClient().PostAsJsonAsync("/api/leads", new
        {
            name = "Ada Client",
            email,
            projectType = "ECommerce",
            budget = "From5kTo15k",
            message = "We need a webshop with Stripe payments."
        });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var admin = await app.CreateAdminClientAsync();
        var leads = await admin.GetFromJsonAsync<List<Lead>>("/api/admin/leads", Json.Options);
        var lead = Assert.Single(leads!, l => l.Email == email);
        Assert.Equal(ProjectType.ECommerce, lead.ProjectType);
        Assert.Contains(app.Mail.Sent, m => m.To == email);
    }

    [Fact]
    public async Task Honeypot_submission_is_silently_dropped()
    {
        var email = $"bot-{Guid.NewGuid():N}@example.com";
        var response = await app.CreateClient().PostAsJsonAsync("/api/leads", new
        {
            name = "Bot", email, message = "Buy cheap things now!!!", website = "http://spam.example"
        });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var admin = await app.CreateAdminClientAsync();
        var leads = await admin.GetFromJsonAsync<List<Lead>>("/api/admin/leads", Json.Options);
        Assert.DoesNotContain(leads!, l => l.Email == email);
    }

    [Fact]
    public async Task Clients_cannot_read_leads()
    {
        var client = await app.CreateUserClientAsync($"c-{Guid.NewGuid():N}@example.com");
        var response = await client.GetAsync("/api/admin/leads");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}

[Collection(AppCollection.Name)]
public sealed class ProjectApiTests(AppFactory app)
{
    private static object NewProject(string title = "Fix checkout crash") => new
    {
        title,
        type = "BugFix",
        budget = "Under2k",
        description = "The checkout page crashes on iPhone when the coupon field is used.",
        existingUrl = "https://shop.example.com/checkout"
    };

    [Fact]
    public async Task Anonymous_requests_are_rejected()
    {
        var response = await app.CreateClient().GetAsync("/api/projects");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Client_creates_and_reads_own_project()
    {
        var client = await app.CreateUserClientAsync($"owner-{Guid.NewGuid():N}@example.com");

        var created = await client.PostAsJsonAsync("/api/projects", NewProject());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.ReadAsync<JsonElement>()).GetProperty("id").GetGuid();

        var list = await client.GetFromJsonAsync<List<ProjectSummaryDto>>("/api/projects", Json.Options);
        var summary = Assert.Single(list!);
        Assert.Equal(id, summary.Id);
        Assert.Equal(ProjectStatus.Submitted, summary.Status);

        var detail = await client.GetFromJsonAsync<ProjectDetailDto>($"/api/projects/{id}", Json.Options);
        Assert.Equal("https://shop.example.com/checkout", detail!.ExistingUrl);
        Assert.Single(detail.Events);
    }

    [Fact]
    public async Task Invalid_project_is_rejected()
    {
        var client = await app.CreateUserClientAsync($"inv-{Guid.NewGuid():N}@example.com");
        var response = await client.PostAsJsonAsync("/api/projects", new { title = "x", type = "Website", description = "short", existingUrl = "ftp://nope" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.ReadAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("Title", out _));
        Assert.True(errors.TryGetProperty("Description", out _));
        Assert.True(errors.TryGetProperty("ExistingUrl", out _));
    }

    [Fact]
    public async Task Other_clients_cannot_see_or_message_a_project()
    {
        var owner = await app.CreateUserClientAsync($"a-{Guid.NewGuid():N}@example.com");
        var stranger = await app.CreateUserClientAsync($"b-{Guid.NewGuid():N}@example.com");
        var id = (await (await owner.PostAsJsonAsync("/api/projects", NewProject())).ReadAsync<JsonElement>()).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/projects/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await stranger.PostAsJsonAsync($"/api/projects/{id}/messages", new { body = "hello" })).StatusCode);
        Assert.Empty((await stranger.GetFromJsonAsync<List<ProjectSummaryDto>>("/api/projects", Json.Options))!);
    }

    [Fact]
    public async Task Messages_and_status_changes_flow_between_client_and_team()
    {
        var owner = await app.CreateUserClientAsync($"flow-{Guid.NewGuid():N}@example.com");
        var admin = await app.CreateAdminClientAsync();
        var id = (await (await owner.PostAsJsonAsync("/api/projects", NewProject())).ReadAsync<JsonElement>()).GetProperty("id").GetGuid();

        // Clients cannot change status.
        var forbidden = await owner.PutAsJsonAsync($"/api/projects/{id}/status", new { status = "Completed" });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        (await owner.PostAsJsonAsync($"/api/projects/{id}/messages", new { body = "Any update?" })).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync($"/api/projects/{id}/messages", new { body = "Reproduced it, fixing now." })).EnsureSuccessStatusCode();
        var status = await admin.PutAsJsonAsync($"/api/projects/{id}/status", new { status = "InProgress", note = "Root cause found" });
        Assert.Equal(HttpStatusCode.NoContent, status.StatusCode);

        var detail = await owner.GetFromJsonAsync<ProjectDetailDto>($"/api/projects/{id}", Json.Options);
        Assert.Equal(ProjectStatus.InProgress, detail!.Status);
        Assert.Equal(2, detail.Messages.Count);
        Assert.True(detail.Messages[1].IsFromStaff);
        Assert.Contains(detail.Events, e => e.Text.Contains("In progress") && e.Text.Contains("Root cause found"));
    }
}

[Collection(AppCollection.Name)]
public sealed class InvoiceApiTests(AppFactory app)
{
    private async Task<(HttpClient Owner, HttpClient Admin, InvoiceDto Invoice)> CreateInvoiceAsync()
    {
        var owner = await app.CreateUserClientAsync($"pay-{Guid.NewGuid():N}@example.com");
        var admin = await app.CreateAdminClientAsync();
        var projectId = (await (await owner.PostAsJsonAsync("/api/projects", new
        {
            title = "Company website",
            type = "Website",
            description = "Five pages, contact form, dark and light theme."
        })).ReadAsync<JsonElement>()).GetProperty("id").GetGuid();

        var created = await admin.PostAsJsonAsync("/api/invoices", new
        {
            projectId,
            currency = "eur",
            dueInDays = 14,
            lines = new[]
            {
                new { description = "Design phase", quantity = 1, unitAmountMinor = 150_000L },
                new { description = "Hosting setup", quantity = 2, unitAmountMinor = 25_000L }
            }
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var invoice = (await created.ReadAsync<InvoiceDto>())!;
        return (owner, admin, invoice);
    }

    private static HttpRequestMessage SignedWebhook(string payload, string secret)
    {
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = StripeWebhookVerifier.ComputeSignature(payload, ts, secret);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/stripe")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Stripe-Signature", $"t={ts.ToString(CultureInfo.InvariantCulture)},v1={signature}");
        return request;
    }

    private static string CheckoutCompleted(string eventId, Guid invoiceId) => JsonSerializer.Serialize(new
    {
        id = eventId,
        type = "checkout.session.completed",
        data = new
        {
            @object = new
            {
                id = $"cs_test_{invoiceId:N}",
                payment_status = "paid",
                payment_intent = $"pi_{invoiceId:N}",
                metadata = new { invoice_id = invoiceId.ToString() }
            }
        }
    });

    [Fact]
    public async Task Admin_issues_invoice_with_correct_total_and_number()
    {
        var (owner, _, invoice) = await CreateInvoiceAsync();

        Assert.Equal(200_000, invoice.TotalMinor);
        Assert.Equal(InvoiceStatus.Open, invoice.Status);
        Assert.Matches(@"^INV-\d{4}-\d{4}$", invoice.Number);

        var mine = await owner.GetFromJsonAsync<List<InvoiceDto>>("/api/invoices", Json.Options);
        Assert.Contains(mine!, i => i.Id == invoice.Id);
    }

    [Fact]
    public async Task Clients_cannot_issue_invoices()
    {
        var owner = await app.CreateUserClientAsync($"noinv-{Guid.NewGuid():N}@example.com");
        var response = await owner.PostAsJsonAsync("/api/invoices", new { projectId = Guid.NewGuid(), lines = new[] { new { description = "x", quantity = 1, unitAmountMinor = 100L } } });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Checkout_then_signed_webhook_marks_invoice_paid_idempotently()
    {
        var (owner, _, invoice) = await CreateInvoiceAsync();

        var checkout = await owner.PostAsync($"/api/invoices/{invoice.Id}/checkout", null);
        checkout.EnsureSuccessStatusCode();
        var url = (await checkout.ReadAsync<JsonElement>()).GetProperty("url").GetString();
        Assert.StartsWith("https://checkout.stripe.test/", url);
        var sent = Assert.Single(app.Payments.Checkouts, c => c.InvoiceId == invoice.Id);
        Assert.Equal(2, sent.Lines.Count);
        Assert.StartsWith("https://test.local/portal/invoices/", sent.SuccessUrl);

        var payload = CheckoutCompleted($"evt_{Guid.NewGuid():N}", invoice.Id);
        var client = app.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(SignedWebhook(payload, AppFactory.WebhookSecret))).StatusCode);
        // Stripe retries deliveries; the same event must be accepted and ignored.
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(SignedWebhook(payload, AppFactory.WebhookSecret))).StatusCode);

        var paid = await owner.GetFromJsonAsync<InvoiceDto>($"/api/invoices/{invoice.Id}", Json.Options);
        Assert.Equal(InvoiceStatus.Paid, paid!.Status);
        Assert.NotNull(paid.PaidAt);

        // Paying twice is refused.
        var again = await owner.PostAsync($"/api/invoices/{invoice.Id}/checkout", null);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    [Fact]
    public async Task Webhook_with_bad_signature_is_rejected_and_changes_nothing()
    {
        var (owner, _, invoice) = await CreateInvoiceAsync();
        var payload = CheckoutCompleted($"evt_{Guid.NewGuid():N}", invoice.Id);

        var response = await app.CreateClient().SendAsync(SignedWebhook(payload, "whsec_attacker"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var unchanged = await owner.GetFromJsonAsync<InvoiceDto>($"/api/invoices/{invoice.Id}", Json.Options);
        Assert.Equal(InvoiceStatus.Open, unchanged!.Status);
    }

    [Fact]
    public async Task Admin_can_partially_then_fully_refund()
    {
        var (_, admin, invoice) = await CreateInvoiceAsync();
        (await app.CreateClient().SendAsync(SignedWebhook(CheckoutCompleted($"evt_{Guid.NewGuid():N}", invoice.Id), AppFactory.WebhookSecret)))
            .EnsureSuccessStatusCode();

        var tooMuch = await admin.PostAsJsonAsync($"/api/invoices/{invoice.Id}/refund", new { amountMinor = 999_999 });
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.StatusCode);

        var partial = await (await admin.PostAsJsonAsync($"/api/invoices/{invoice.Id}/refund", new { amountMinor = 50_000 })).ReadAsync<InvoiceDto>();
        Assert.Equal(InvoiceStatus.Paid, partial!.Status);
        Assert.Equal(50_000, partial.RefundedAmountMinor);

        var full = await (await admin.PostAsJsonAsync($"/api/invoices/{invoice.Id}/refund", new { })).ReadAsync<InvoiceDto>();
        Assert.Equal(InvoiceStatus.Refunded, full!.Status);
        Assert.Contains(app.Payments.Refunds, r => r.PaymentIntentId == $"pi_{invoice.Id:N}" && r.Amount == 150_000);
    }
}

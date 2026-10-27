using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Novaforge.Web.Services;

namespace Novaforge.Tests;

/// <summary>Runs the real app in-memory against a throw-away SQLite database, with e-mail and Stripe faked.</summary>
public sealed class AppFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@test.local";
    public const string AdminPassword = "Admin-Pass-12345";
    public const string WebhookSecret = "whsec_test_secret_value";

    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"novaforge-test-{Guid.NewGuid():N}.db");

    public RecordingNotificationSender Mail { get; } = new();
    public FakePaymentGateway Payments { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={dbPath}");
        builder.UseSetting("Admin:Email", AdminEmail);
        builder.UseSetting("Admin:Password", AdminPassword);
        builder.UseSetting("Admin:FullName", "Test Admin");
        builder.UseSetting("Stripe:WebhookSecret", WebhookSecret);
        builder.UseSetting("Site:BaseUrl", "https://test.local");
        builder.UseSetting("RateLimits:PublicPerMinute", "1000");
        builder.UseSetting("RateLimits:AuthPerMinute", "1000");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<INotificationSender>();
            services.AddSingleton<INotificationSender>(Mail);
            services.RemoveAll<IPaymentGateway>();
            services.AddSingleton<IPaymentGateway>(Payments);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        foreach (var file in new[] { dbPath, dbPath + "-wal", dbPath + "-shm" })
        {
            try { File.Delete(file); } catch (IOException) { /* best effort */ }
        }
    }

    public async Task<HttpClient> CreateUserClientAsync(string email, string password = "Client-Pass-12345")
    {
        var anon = CreateClient();
        var register = await anon.PostAsJsonAsync("/api/auth/register", new { email, password });
        register.EnsureSuccessStatusCode();
        return await LoginAsync(email, password);
    }

    public Task<HttpClient> CreateAdminClientAsync() => LoginAsync(AdminEmail, AdminPassword);

    private async Task<HttpClient> LoginAsync(string email, string password)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.GetProperty("accessToken").GetString());
        return client;
    }
}

public static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static Task<T?> ReadAsync<T>(this HttpResponseMessage response) =>
        response.Content.ReadFromJsonAsync<T>(Options);
}

public sealed class RecordingNotificationSender : INotificationSender
{
    public ConcurrentQueue<(string To, string Subject, string Body)> Sent { get; } = new();

    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        Sent.Enqueue((to, subject, body));
        return Task.CompletedTask;
    }
}

public sealed class FakePaymentGateway : IPaymentGateway
{
    public bool IsConfigured => true;
    public ConcurrentQueue<CheckoutRequest> Checkouts { get; } = new();
    public ConcurrentQueue<(string PaymentIntentId, long Amount)> Refunds { get; } = new();

    public Task<CheckoutSession> CreateCheckoutAsync(CheckoutRequest request, CancellationToken ct = default)
    {
        Checkouts.Enqueue(request);
        var id = $"cs_test_{request.InvoiceId:N}";
        return Task.FromResult(new CheckoutSession(id, $"https://checkout.stripe.test/{id}"));
    }

    public Task RefundAsync(string paymentIntentId, long amountMinor, string idempotencyKey, CancellationToken ct = default)
    {
        Refunds.Enqueue((paymentIntentId, amountMinor));
        return Task.CompletedTask;
    }
}

[CollectionDefinition(Name)]
public sealed class AppCollection : ICollectionFixture<AppFactory>
{
    public const string Name = "app";
}

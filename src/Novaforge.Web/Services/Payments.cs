using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Novaforge.Web.Services;

public sealed record CheckoutLine(string Description, int Quantity, long UnitAmountMinor);

public sealed record CheckoutRequest(
    Guid InvoiceId, string InvoiceNumber, string Currency, string? CustomerEmail,
    IReadOnlyList<CheckoutLine> Lines, string SuccessUrl, string CancelUrl);

public sealed record CheckoutSession(string SessionId, string Url);

public interface IPaymentGateway
{
    bool IsConfigured { get; }
    Task<CheckoutSession> CreateCheckoutAsync(CheckoutRequest request, CancellationToken ct = default);
    Task RefundAsync(string paymentIntentId, long amountMinor, string idempotencyKey, CancellationToken ct = default);
}

public sealed class PaymentGatewayException(string message) : Exception(message);

/// <summary>
/// Stripe integration over plain HTTPS (no SDK dependency): hosted Checkout for invoice payment and Refunds.
/// Card data never touches this server, which keeps PCI scope at SAQ A.
/// </summary>
public sealed class StripePaymentGateway(HttpClient http, IOptions<StripeOptions> options, ILogger<StripePaymentGateway> logger)
    : IPaymentGateway
{
    public bool IsConfigured => options.Value.IsConfigured;

    public async Task<CheckoutSession> CreateCheckoutAsync(CheckoutRequest request, CancellationToken ct = default)
    {
        EnsureConfigured();
        var form = new List<KeyValuePair<string, string>>
        {
            new("mode", "payment"),
            new("success_url", request.SuccessUrl),
            new("cancel_url", request.CancelUrl),
            new("client_reference_id", request.InvoiceId.ToString()),
            new("metadata[invoice_id]", request.InvoiceId.ToString()),
            new("metadata[invoice_number]", request.InvoiceNumber),
            new("payment_intent_data[metadata][invoice_id]", request.InvoiceId.ToString()),
            new("payment_intent_data[description]", $"Invoice {request.InvoiceNumber}"),
        };
        if (!string.IsNullOrEmpty(request.CustomerEmail))
        {
            form.Add(new("customer_email", request.CustomerEmail));
        }
        for (var i = 0; i < request.Lines.Count; i++)
        {
            var line = request.Lines[i];
            form.Add(new($"line_items[{i}][quantity]", line.Quantity.ToString(CultureInfo.InvariantCulture)));
            form.Add(new($"line_items[{i}][price_data][currency]", request.Currency));
            form.Add(new($"line_items[{i}][price_data][unit_amount]", line.UnitAmountMinor.ToString(CultureInfo.InvariantCulture)));
            form.Add(new($"line_items[{i}][price_data][product_data][name]", line.Description));
        }

        // A new session per click is fine; the key only de-duplicates network retries of the same click.
        var idempotencyKey = $"checkout-{request.InvoiceId}-{Guid.NewGuid():N}";
        using var doc = await PostAsync("/v1/checkout/sessions", form, idempotencyKey, ct);
        var root = doc.RootElement;
        return new CheckoutSession(root.GetProperty("id").GetString()!, root.GetProperty("url").GetString()!);
    }

    public async Task RefundAsync(string paymentIntentId, long amountMinor, string idempotencyKey, CancellationToken ct = default)
    {
        EnsureConfigured();
        var form = new List<KeyValuePair<string, string>>
        {
            new("payment_intent", paymentIntentId),
            new("amount", amountMinor.ToString(CultureInfo.InvariantCulture)),
        };
        using var _ = await PostAsync("/v1/refunds", form, idempotencyKey, ct);
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured)
        {
            throw new PaymentGatewayException("Online payments are not configured yet. Please contact us to pay by bank transfer.");
        }
    }

    private async Task<JsonDocument> PostAsync(string path, List<KeyValuePair<string, string>> form, string idempotencyKey, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, options.Value.ApiBase.TrimEnd('/') + path)
        {
            Content = new FormUrlEncodedContent(form)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.SecretKey);
        request.Headers.Add("Idempotency-Key", idempotencyKey);

        using var response = await http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            // Stripe error bodies contain no secrets; log them for diagnosis but show the user a generic message.
            logger.LogError("Stripe {Path} failed with {Status}: {Body}", path, (int)response.StatusCode, body);
            throw new PaymentGatewayException("The payment provider rejected the request. Please try again or contact us.");
        }
        return JsonDocument.Parse(body);
    }
}

/// <summary>Verifies the Stripe-Signature header (HMAC-SHA256 over "{timestamp}.{payload}").</summary>
public static class StripeWebhookVerifier
{
    public static bool IsValid(string payload, string? signatureHeader, string secret, DateTimeOffset now,
        TimeSpan? tolerance = null)
    {
        if (string.IsNullOrEmpty(signatureHeader) || string.IsNullOrEmpty(secret))
        {
            return false;
        }

        long? timestamp = null;
        var signatures = new List<string>();
        foreach (var part in signatureHeader.Split(','))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2)
            {
                continue;
            }
            var key = kv[0].Trim();
            if (key == "t" && long.TryParse(kv[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var t))
            {
                timestamp = t;
            }
            else if (key == "v1")
            {
                signatures.Add(kv[1].Trim());
            }
        }

        if (timestamp is null || signatures.Count == 0)
        {
            return false;
        }

        var age = now - DateTimeOffset.FromUnixTimeSeconds(timestamp.Value);
        if (age.Duration() > (tolerance ?? TimeSpan.FromMinutes(5)))
        {
            return false; // replay protection
        }

        var expected = ComputeSignature(payload, timestamp.Value, secret);
        var expectedBytes = Encoding.ASCII.GetBytes(expected);
        return signatures.Any(s => CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(s), expectedBytes));
    }

    public static string ComputeSignature(string payload, long timestamp, string secret)
    {
        var signedPayload = $"{timestamp.ToString(CultureInfo.InvariantCulture)}.{payload}";
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(signedPayload));
        return Convert.ToHexStringLower(hash);
    }
}

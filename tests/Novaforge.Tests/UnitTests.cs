using Novaforge.Web.Domain;
using Novaforge.Web.Services;

namespace Novaforge.Tests;

public sealed class StripeWebhookVerifierTests
{
    private const string Secret = "whsec_unit";
    private const string Payload = """{"id":"evt_1","type":"checkout.session.completed"}""";
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

    [Fact]
    public void Accepts_valid_signature()
    {
        var ts = Now.ToUnixTimeSeconds();
        var header = $"t={ts},v1={StripeWebhookVerifier.ComputeSignature(Payload, ts, Secret)}";
        Assert.True(StripeWebhookVerifier.IsValid(Payload, header, Secret, Now));
    }

    [Fact]
    public void Accepts_when_one_of_several_signatures_matches()
    {
        var ts = Now.ToUnixTimeSeconds();
        var header = $"t={ts},v1=deadbeef,v1={StripeWebhookVerifier.ComputeSignature(Payload, ts, Secret)},v0=ignored";
        Assert.True(StripeWebhookVerifier.IsValid(Payload, header, Secret, Now));
    }

    [Fact]
    public void Rejects_tampered_payload()
    {
        var ts = Now.ToUnixTimeSeconds();
        var header = $"t={ts},v1={StripeWebhookVerifier.ComputeSignature(Payload, ts, Secret)}";
        Assert.False(StripeWebhookVerifier.IsValid(Payload.Replace("evt_1", "evt_2"), header, Secret, Now));
    }

    [Fact]
    public void Rejects_wrong_secret()
    {
        var ts = Now.ToUnixTimeSeconds();
        var header = $"t={ts},v1={StripeWebhookVerifier.ComputeSignature(Payload, ts, "whsec_other")}";
        Assert.False(StripeWebhookVerifier.IsValid(Payload, header, Secret, Now));
    }

    [Fact]
    public void Rejects_replayed_old_event()
    {
        var ts = Now.AddMinutes(-10).ToUnixTimeSeconds();
        var header = $"t={ts},v1={StripeWebhookVerifier.ComputeSignature(Payload, ts, Secret)}";
        Assert.False(StripeWebhookVerifier.IsValid(Payload, header, Secret, Now));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("t=abc,v1=00")]
    public void Rejects_malformed_header(string? header) =>
        Assert.False(StripeWebhookVerifier.IsValid(Payload, header, Secret, Now));

    [Fact]
    public void Matches_independently_computed_hmac()
    {
        // echo -n "1.payload" | openssl dgst -sha256 -hmac secret
        Assert.Equal(
            "0cb97d5610dee9645ed2ef52bff5370e436d4414b426199dc8c9aacc001d6c14",
            StripeWebhookVerifier.ComputeSignature("payload", 1, "secret"));
    }
}

public sealed class DomainTests
{
    [Fact]
    public void Invoice_total_is_sum_of_lines_in_minor_units()
    {
        var invoice = new Invoice
        {
            Lines =
            [
                new InvoiceLine { Quantity = 2, UnitAmountMinor = 150_000 },
                new InvoiceLine { Quantity = 1, UnitAmountMinor = 99_95 }
            ]
        };
        Assert.Equal(309_995, invoice.TotalMinor);
    }

    [Fact]
    public void Invoice_is_overdue_only_when_open_and_past_due()
    {
        var now = new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);
        var invoice = new Invoice { Status = InvoiceStatus.Open, DueDate = now.AddDays(-1) };
        Assert.True(invoice.IsOverdue(now));

        invoice.Status = InvoiceStatus.Paid;
        Assert.False(invoice.IsOverdue(now));

        invoice.Status = InvoiceStatus.Open;
        invoice.DueDate = now.AddDays(1);
        Assert.False(invoice.IsOverdue(now));
    }

    [Theory]
    [InlineData(123450, "eur", "€1,234.50")]
    [InlineData(5, "usd", "$0.05")]
    [InlineData(100000, "dkk", "1,000.00 DKK")]
    public void Money_formats_minor_units(long minor, string currency, string expected) =>
        Assert.Equal(expected, DisplayText.Money(minor, currency));
}

public sealed class RequestValidatorTests
{
    [Fact]
    public void Validates_nested_invoice_lines()
    {
        var request = new CreateInvoiceRequest
        {
            ProjectId = Guid.NewGuid(),
            Lines = [new InvoiceLineRequest { Description = "", Quantity = 0, UnitAmountMinor = 0 }]
        };

        var ex = Assert.Throws<ValidationFailedException>(() => RequestValidator.Validate(request));
        Assert.Contains("Lines[0].Description", ex.Errors.Keys);
        Assert.Contains("Lines[0].Quantity", ex.Errors.Keys);
        Assert.Contains("Lines[0].UnitAmountMinor", ex.Errors.Keys);
    }

    [Fact]
    public void Optional_url_accepts_blank_and_rejects_non_http()
    {
        var ok = new CreateProjectRequest { Title = "Site", Description = "A description that is long enough.", ExistingUrl = "" };
        RequestValidator.Validate(ok);

        var bad = new CreateProjectRequest { Title = "Site", Description = "A description that is long enough.", ExistingUrl = "javascript:alert(1)" };
        var ex = Assert.Throws<ValidationFailedException>(() => RequestValidator.Validate(bad));
        Assert.Contains(nameof(CreateProjectRequest.ExistingUrl), ex.Errors.Keys);
    }
}

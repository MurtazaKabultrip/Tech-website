using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Novaforge.Web.Services;

public interface INotificationSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}

/// <summary>Sends plain-text e-mail via SMTP when configured; otherwise logs. Failures never break the request.</summary>
public sealed class EmailNotificationSender(IOptions<SmtpOptions> options, ILogger<EmailNotificationSender> logger)
    : INotificationSender
{
    public async Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        var smtp = options.Value;
        if (!smtp.IsConfigured)
        {
            logger.LogInformation("E-mail (SMTP not configured) to {To}: {Subject}", to, subject);
            return;
        }

        try
        {
            using var client = new SmtpClient(smtp.Host, smtp.Port) { EnableSsl = smtp.EnableSsl };
            if (!string.IsNullOrEmpty(smtp.Username))
            {
                client.Credentials = new NetworkCredential(smtp.Username, smtp.Password);
            }
            using var message = new MailMessage(smtp.From, to, subject, body);
            await client.SendMailAsync(message, ct);
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException or FormatException)
        {
            logger.LogError(ex, "Failed to send e-mail to {To}: {Subject}", to, subject);
        }
    }
}

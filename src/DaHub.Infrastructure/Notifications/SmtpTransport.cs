using System.Net;
using System.Net.Mail;
using DaHub.Core.Notifications;

namespace DaHub.Infrastructure.Notifications;

public interface ISmtpDelivery
{
    Task SendAsync(MailMessage message, CancellationToken cancellationToken);
}

// Anonymous STARTTLS relay through an administrator-approved Exchange connector.
// No password fallback or certificate-validation override.
public sealed class SmtpRelayDelivery(string host, int port) : ISmtpDelivery
{
    public async Task SendAsync(MailMessage message, CancellationToken cancellationToken)
    {
        using var client = new SmtpClient(host, port) { EnableSsl = true, UseDefaultCredentials = false, Timeout = 60000 };
        await client.SendMailAsync(message, cancellationToken);
    }
}

public sealed class SmtpTransport(ISmtpDelivery delivery, string from,
    IReadOnlySet<string> recipients) : INotificationTransport
{
    public string Channel => "Email";
    public async Task<DeliveryResult> SendAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        if (!recipients.Contains(message.Recipient)) return new(DeliveryOutcome.PermanentFailure);
        using var mail = new MailMessage(from, message.Recipient)
        {
            Subject = message.Subject,
            Body = "<html><body><pre>" + WebUtility.HtmlEncode(message.Body) + "</pre></body></html>",
            IsBodyHtml = true
        };
        try
        {
            await delivery.SendAsync(mail, cancellationToken);
            return new(DeliveryOutcome.Accepted);
        }
        catch (SmtpException error) when ((int)error.StatusCode >= 400 && (int)error.StatusCode < 500)
        {
            return new(DeliveryOutcome.TransientFailure);
        }
        catch (SmtpException error) when ((int)error.StatusCode >= 500)
        {
            return new(DeliveryOutcome.PermanentFailure);
        }
        // Transport exceptions and disconnects can follow acceptance. Let the handler
        // record uncertainty; never include exception text in notification evidence.
    }
}

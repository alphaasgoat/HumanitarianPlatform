using System.Net;
using System.Net.Mail;
using System.Text;
using Humanitarian.Application.Notifications;
using Humanitarian.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Humanitarian.Infrastructure.Notifications;

/// <summary>
/// E-mail channel (adapter for an SMTP server). With no SMTP host configured it writes the message to the log and
/// reports provider "log", so the whole workflow can be demonstrated without a mail server and nothing pretends
/// to have been delivered by SMTP. Any SMTP failure is returned as a failed result; the dispatcher retries it.
/// </summary>
public class EmailChannel(IOptions<EmailSettings> options, ILogger<EmailChannel> log) : INotificationChannel
{
    public NotificationChannel Channel => NotificationChannel.Email;

    public async Task<ChannelSendResult> SendAsync(OutboundMessage message, CancellationToken ct = default)
    {
        var s = options.Value;
        if (!s.IsConfigured)
        {
            log.LogInformation("EMAIL (no SMTP server configured, logged only) to {To} | {Subject} | {Body}",
                message.Destination, message.Subject, message.Body);
            return new ChannelSendResult(true, "log");
        }

        try
        {
            using var client = new SmtpClient(s.Host, s.Port)
            {
                EnableSsl = s.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 15000
            };
            if (!string.IsNullOrEmpty(s.Username)) client.Credentials = new NetworkCredential(s.Username, s.Password);

            using var mail = new MailMessage
            {
                From = new MailAddress(s.FromAddress, s.FromName),
                Subject = message.Subject,
                Body = message.Body,
                IsBodyHtml = false,
                SubjectEncoding = Encoding.UTF8,
                BodyEncoding = Encoding.UTF8
            };
            mail.To.Add(message.Destination);

            await client.SendMailAsync(mail, ct);
            return new ChannelSendResult(true, "smtp");
        }
        catch (FormatException ex)
        {
            // A malformed address will never work: park the message instead of retrying it five times.
            return new ChannelSendResult(false, "smtp", ChannelSendResult.PermanentPrefix + "invalid e-mail address (" + ex.Message + ")");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ChannelSendResult(false, "smtp", $"{ex.GetType().Name}: {ex.Message}");
        }
    }
}

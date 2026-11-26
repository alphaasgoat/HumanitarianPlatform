using Humanitarian.Application.Notifications;
using Humanitarian.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Humanitarian.Infrastructure.Notifications;

/// <summary>
/// SMS channel with NO real provider behind it: it logs the text and reports provider "sms-simulated". It shows that a
/// second channel plugs into the same strategy/factory and outbox; a real gateway would replace only this class.
/// </summary>
public class SimulatedSmsChannel(ILogger<SimulatedSmsChannel> log) : INotificationChannel
{
    public NotificationChannel Channel => NotificationChannel.Sms;

    public Task<ChannelSendResult> SendAsync(OutboundMessage message, CancellationToken ct = default)
    {
        var text = message.Body.Length <= 160 ? message.Body : message.Body[..157] + "...";
        var to = message.Destination.Length <= 4 ? "****" : new string('*', message.Destination.Length - 4) + message.Destination[^4..];
        log.LogInformation("SMS (simulated, no provider) to {To} | {Text}", to, text);
        return Task.FromResult(new ChannelSendResult(true, "sms-simulated"));
    }
}

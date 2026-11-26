using Humanitarian.Domain.Common;
using Humanitarian.Domain.Enums;

namespace Humanitarian.Domain.Entities;

/// <summary>
/// What a person is told (the in-app inbox entry and the record of the communication). The text is rendered once,
/// in the recipient's language, when the event happens. A unique (recipient, DedupeKey) pair means the same event
/// can never notify the same person twice, even if it is processed again.
/// </summary>
public class Notification : BaseEntity
{
    public Guid RecipientUserId { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string Language { get; set; } = "en";

    /// <summary>Identifies the event that caused this notification (for example "request-status:{eventId}").</summary>
    public string DedupeKey { get; set; } = "";

    public string? RelatedEntityType { get; set; }
    public string? RelatedEntityId { get; set; }
    public DateTime? ReadAtUtc { get; set; }
}

/// <summary>
/// One outbound message (an e-mail or an SMS) for a notification: the transactional outbox. It is saved in the same
/// transaction as the business change, and a background worker sends it with retries.
/// </summary>
public class NotificationDelivery : BaseEntity
{
    public Guid NotificationId { get; set; }
    public NotificationChannel Channel { get; set; }
    public string Destination { get; set; } = "";
    public DeliveryStatus Status { get; set; } = DeliveryStatus.Pending;
    public int Attempts { get; set; }
    public DateTime NextAttemptUtc { get; set; }

    /// <summary>Lease: a worker that crashes while sending lets another worker pick the message up after this time.</summary>
    public DateTime? LockedUntilUtc { get; set; }

    public DateTime? SentAtUtc { get; set; }

    /// <summary>Which implementation sent it ("smtp", "log", "sms-simulated"), so a demo never pretends to be real.</summary>
    public string? Provider { get; set; }

    public string? LastError { get; set; }

    /// <summary>Optimistic-concurrency token: two workers cannot claim the same message.</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

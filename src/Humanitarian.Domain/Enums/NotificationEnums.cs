namespace Humanitarian.Domain.Enums;

public enum NotificationType
{
    RequestReceived = 1,
    RequestTriaged,
    RequestEscalated,
    RequestMerged,
    RequestClosed,
    ShelterPlacement,
    VolunteerAssignment,
    Announcement
}

public enum NotificationChannel { Email = 1, Sms }

/// <summary>Pending -> Processing (claimed by a worker) -> Sent, or back to Pending for a retry, or Failed.</summary>
public enum DeliveryStatus { Pending, Processing, Sent, Failed }

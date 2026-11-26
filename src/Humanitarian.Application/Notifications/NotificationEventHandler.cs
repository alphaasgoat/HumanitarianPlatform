using System.Globalization;
using Humanitarian.Application.Common;
using Humanitarian.Domain.Enums;

namespace Humanitarian.Application.Notifications;

/// <summary>
/// Subscriber (Observer pattern): turns domain events into notifications. It only stages rows in the current unit of
/// work, so the notification is saved in the same transaction as the change that caused it (transactional outbox).
/// The dedupe key is built from the event id, so processing the same event again notifies nobody twice.
/// </summary>
public class NotificationEventHandler(INotificationService notifications) :
    IDomainEventHandler<RequestStatusChanged>,
    IDomainEventHandler<ShelterPlacementMade>,
    IDomainEventHandler<VolunteerAssigned>
{
    public async Task HandleAsync(RequestStatusChanged e, CancellationToken ct = default)
    {
        var type = e.To switch
        {
            RequestStatus.UnderReview when e.From is null or RequestStatus.Submitted => NotificationType.RequestReceived,
            RequestStatus.Triaged => NotificationType.RequestTriaged,
            RequestStatus.Escalated => NotificationType.RequestEscalated,
            RequestStatus.Merged => NotificationType.RequestMerged,
            RequestStatus.Closed => NotificationType.RequestClosed,
            _ => (NotificationType?)null
        };
        if (type is null) return; // not every internal step is worth telling the person about

        await notifications.EnqueueAsync(new NotificationRequest(
            e.RequesterId, type.Value, $"request-status:{e.EventId}",
            new Dictionary<string, string> { ["ref"] = e.RequestId.ToString("N")[..8] },
            "AssistanceRequest", e.RequestId.ToString()), ct);
    }

    public async Task HandleAsync(ShelterPlacementMade e, CancellationToken ct = default) =>
        await notifications.EnqueueAsync(new NotificationRequest(
            e.RequesterId, NotificationType.ShelterPlacement, $"shelter-placement:{e.EventId}",
            new Dictionary<string, string>
            {
                ["people"] = e.People.ToString(CultureInfo.InvariantCulture),
                ["shelter"] = e.ShelterName,
                ["address"] = e.ShelterAddress
            },
            "AssistanceRequest", e.RequestId.ToString()), ct);

    public async Task HandleAsync(VolunteerAssigned e, CancellationToken ct = default) =>
        await notifications.EnqueueAsync(new NotificationRequest(
            e.VolunteerUserId, NotificationType.VolunteerAssignment, $"volunteer-assigned:{e.EventId}",
            new Dictionary<string, string>
            {
                ["task"] = e.TaskTitle,
                ["start"] = Format(e.StartUtc),
                ["end"] = Format(e.EndUtc)
            },
            "VolunteerTask", e.TaskId.ToString()), ct);

    private static string Format(DateTime utc) => utc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC";
}

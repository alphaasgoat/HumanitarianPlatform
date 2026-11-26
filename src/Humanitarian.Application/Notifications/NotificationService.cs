using Humanitarian.Application.Common;
using Humanitarian.Domain.Entities;
using Humanitarian.Domain.Enums;

namespace Humanitarian.Application.Notifications;

/// <summary>Everything an event handler needs to say to one person.</summary>
public record NotificationRequest(
    Guid RecipientUserId, NotificationType Type, string DedupeKey, IReadOnlyDictionary<string, string> Args,
    string? RelatedEntityType = null, string? RelatedEntityId = null, string? TitleOverride = null, string? BodyOverride = null);

public interface INotificationService
{
    /// <summary>Stages the notification and its outbound messages in the current unit of work (the caller commits).
    /// Returns false, and stages nothing, if this event already notified this person.</summary>
    Task<bool> EnqueueAsync(NotificationRequest request, CancellationToken ct = default);

    Task<NotificationPage> ListMineAsync(bool unreadOnly, int page, int pageSize, CancellationToken ct = default);
    Task<int> UnreadCountAsync(CancellationToken ct = default);
    Task MarkReadAsync(Guid id, CancellationToken ct = default);
    Task<int> MarkAllReadAsync(CancellationToken ct = default);

    Task<AnnouncementResultDto> AnnounceAsync(AnnouncementDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<DeliveryDto>> ListDeliveriesAsync(DeliveryStatus? status, int take, CancellationToken ct = default);
    Task<DeliveryDto> RetryDeliveryAsync(Guid deliveryId, CancellationToken ct = default);
}

public class NotificationService(
    IUnitOfWork uow, IAuditService audit, ICurrentUser current, TimeProvider time) : INotificationService
{
    private const int MaxAnnouncementRecipients = 5000;

    // SMS costs money and is intrusive: only for things a person should not miss.
    private static readonly HashSet<NotificationType> SmsTypes =
        [NotificationType.ShelterPlacement, NotificationType.VolunteerAssignment, NotificationType.Announcement];

    private DateTime Now => time.GetUtcNow().UtcDateTime;
    private bool CanOperate => current.Role is Roles.EmergencyCoordinator or Roles.SystemAdministrator;

    // =====================================================================
    // Staging (called by event handlers inside the business transaction)
    // =====================================================================
    public async Task<bool> EnqueueAsync(NotificationRequest request, CancellationToken ct = default)
    {
        var recipient = await uow.Repository<User>().GetByIdAsync(request.RecipientUserId, ct);
        if (recipient is null || !recipient.IsActive) return false;

        // Duplicate prevention: the same event never notifies the same person twice.
        var notifications = uow.Repository<Notification>();
        if (await notifications.AnyAsync(n => n.RecipientUserId == recipient.Id && n.DedupeKey == request.DedupeKey, ct))
            return false;

        var language = NotificationTemplates.NormalizeLanguage(recipient.PreferredLanguage);
        string title, body;
        if (request.TitleOverride is not null && request.BodyOverride is not null)
            (title, body) = (request.TitleOverride, request.BodyOverride);
        else
            (title, body) = NotificationTemplates.Render(request.Type, language, request.Args);

        var now = Now;
        var notification = new Notification
        {
            RecipientUserId = recipient.Id,
            Type = request.Type,
            Title = title,
            Body = body,
            Language = language,
            DedupeKey = request.DedupeKey,
            RelatedEntityType = request.RelatedEntityType,
            RelatedEntityId = request.RelatedEntityId,
            CreatedAtUtc = now
        };
        await notifications.AddAsync(notification, ct);

        var deliveries = uow.Repository<NotificationDelivery>();
        if (!string.IsNullOrWhiteSpace(recipient.Email))
            await deliveries.AddAsync(NewDelivery(notification.Id, NotificationChannel.Email, recipient.Email, now), ct);
        if (!string.IsNullOrWhiteSpace(recipient.Phone) && SmsTypes.Contains(request.Type))
            await deliveries.AddAsync(NewDelivery(notification.Id, NotificationChannel.Sms, recipient.Phone!, now), ct);

        return true;
    }

    private static NotificationDelivery NewDelivery(Guid notificationId, NotificationChannel channel, string destination, DateTime now) =>
        new()
        {
            NotificationId = notificationId,
            Channel = channel,
            Destination = destination,
            Status = DeliveryStatus.Pending,
            NextAttemptUtc = now,
            CreatedAtUtc = now
        };

    // =====================================================================
    // Inbox (every signed-in person sees only their own)
    // =====================================================================
    public async Task<NotificationPage> ListMineAsync(bool unreadOnly, int page, int pageSize, CancellationToken ct = default)
    {
        var userId = RequireUser();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var repo = uow.Repository<Notification>();

        var total = await repo.CountAsync(n => n.RecipientUserId == userId && (!unreadOnly || n.ReadAtUtc == null), ct);
        var unread = await repo.CountAsync(n => n.RecipientUserId == userId && n.ReadAtUtc == null, ct);
        var items = await repo.ListAsync(n => n.RecipientUserId == userId && (!unreadOnly || n.ReadAtUtc == null),
            q => q.OrderByDescending(n => n.CreatedAtUtc), (page - 1) * pageSize, pageSize, ct);
        return new NotificationPage(items.Select(n => ToDto(n)).ToList(), total, unread, page, pageSize);
    }

    public async Task<int> UnreadCountAsync(CancellationToken ct = default)
    {
        var userId = RequireUser();
        return await uow.Repository<Notification>().CountAsync(n => n.RecipientUserId == userId && n.ReadAtUtc == null, ct);
    }

    public async Task MarkReadAsync(Guid id, CancellationToken ct = default)
    {
        var userId = RequireUser();
        var repo = uow.Repository<Notification>();
        var n = await repo.GetByIdAsync(id, ct);
        // Do not reveal that other people's notifications exist.
        if (n is null || n.RecipientUserId != userId) throw new NotFoundException("Notification not found.");
        if (n.ReadAtUtc is not null) return;

        n.ReadAtUtc = Now;
        n.UpdatedAtUtc = n.ReadAtUtc;
        repo.Update(n);
        await uow.SaveChangesAsync(ct);
    }

    public async Task<int> MarkAllReadAsync(CancellationToken ct = default)
    {
        var userId = RequireUser();
        var repo = uow.Repository<Notification>();
        var unread = await repo.ListAsync(n => n.RecipientUserId == userId && n.ReadAtUtc == null, ct: ct);
        var now = Now;
        foreach (var n in unread)
        {
            n.ReadAtUtc = now;
            n.UpdatedAtUtc = now;
            repo.Update(n);
        }
        if (unread.Count > 0) await uow.SaveChangesAsync(ct);
        return unread.Count;
    }

    // =====================================================================
    // Emergency announcements (coordinators / administrators)
    // =====================================================================
    public async Task<AnnouncementResultDto> AnnounceAsync(AnnouncementDto dto, CancellationToken ct = default)
    {
        if (!CanOperate) throw new ForbiddenException("Only emergency coordinators and administrators can send announcements.");
        var title = (dto.Title ?? "").Trim();
        var message = (dto.Message ?? "").Trim();
        if (title.Length is < 3 or > 150) throw new AppValidationException("The title must be 3-150 characters.");
        if (message.Length is < 3 or > 1000) throw new AppValidationException("The message must be 3-1000 characters.");

        List<Guid> recipientIds;
        if (dto.CrisisEventId is { } crisisId)
        {
            if (!await uow.Repository<CrisisEvent>().AnyAsync(c => c.Id == crisisId, ct))
                throw new NotFoundException("Crisis event not found.");
            var requests = await uow.Repository<AssistanceRequest>().ListAsync(r => r.CrisisEventId == crisisId, ct: ct);
            recipientIds = requests.Select(r => r.RequesterId).Distinct().ToList();
        }
        else
        {
            var citizens = await uow.Repository<User>().ListAsync(u => u.Role == UserRole.Citizen && u.IsActive, ct: ct);
            recipientIds = citizens.Select(u => u.Id).ToList();
        }
        if (recipientIds.Count > MaxAnnouncementRecipients)
            throw new AppValidationException($"An announcement can reach at most {MaxAnnouncementRecipients} people at once.");

        var announcementId = Guid.NewGuid();
        var noArgs = new Dictionary<string, string>();
        var queued = 0;
        foreach (var id in recipientIds)
        {
            var staged = await EnqueueAsync(new NotificationRequest(
                id, NotificationType.Announcement, $"announcement:{announcementId}", noArgs,
                "Announcement", announcementId.ToString(), title, message), ct);
            if (staged) queued++;
        }

        await audit.RecordAsync("AnnouncementSent", "Announcement", announcementId.ToString(),
            $"crisis={(dto.CrisisEventId?.ToString() ?? "all")}; recipients={recipientIds.Count}; title={title}", ct: ct);
        await uow.SaveChangesAsync(ct);
        return new AnnouncementResultDto(announcementId, recipientIds.Count, queued);
    }

    // =====================================================================
    // Operations: watch the outbox and recover failed messages
    // =====================================================================
    public async Task<IReadOnlyList<DeliveryDto>> ListDeliveriesAsync(DeliveryStatus? status, int take, CancellationToken ct = default)
    {
        if (!CanOperate) throw new ForbiddenException("Only emergency coordinators and administrators can see message deliveries.");
        take = Math.Clamp(take, 1, 200);
        var items = await uow.Repository<NotificationDelivery>().ListAsync(
            d => status == null || d.Status == status,
            q => q.OrderByDescending(d => d.CreatedAtUtc), take: take, ct: ct);
        return items.Select(d => ToDeliveryDto(d)).ToList();
    }

    public async Task<DeliveryDto> RetryDeliveryAsync(Guid deliveryId, CancellationToken ct = default)
    {
        if (!CanOperate) throw new ForbiddenException("Only emergency coordinators and administrators can retry deliveries.");
        var repo = uow.Repository<NotificationDelivery>();
        var d = await repo.GetByIdAsync(deliveryId, ct) ?? throw new NotFoundException("Delivery not found.");
        if (d.Status != DeliveryStatus.Failed) throw new ConflictException("Only failed deliveries can be retried.");

        d.Status = DeliveryStatus.Pending;
        d.Attempts = 0;
        d.NextAttemptUtc = Now;
        d.LockedUntilUtc = null;
        d.UpdatedAtUtc = Now;
        repo.Update(d);
        await audit.RecordAsync("DeliveryRetryRequested", nameof(NotificationDelivery), d.Id.ToString(), d.LastError, ct: ct);
        await uow.SaveChangesAsync(ct);
        return ToDeliveryDto(d);
    }

    // =====================================================================
    // Helpers
    // =====================================================================
    private Guid RequireUser() => current.UserId ?? throw new ForbiddenException("Authenticated user required.");

    private static NotificationDto ToDto(Notification n) =>
        new(n.Id, n.Type.ToString(), n.Title, n.Body, n.CreatedAtUtc, n.ReadAtUtc, n.RelatedEntityType, n.RelatedEntityId);

    /// <summary>The destination is masked so the operations screen does not expose full e-mail addresses or phone numbers.</summary>
    public static string Mask(string destination)
    {
        if (string.IsNullOrEmpty(destination)) return "";
        var at = destination.IndexOf('@');
        if (at > 0) return destination[0] + "***" + destination[at..];
        return destination.Length <= 4 ? "****" : new string('*', destination.Length - 4) + destination[^4..];
    }

    private static DeliveryDto ToDeliveryDto(NotificationDelivery d) =>
        new(d.Id, d.NotificationId, d.Channel.ToString(), Mask(d.Destination), d.Status.ToString(), d.Attempts,
            d.NextAttemptUtc, d.SentAtUtc, d.Provider, d.LastError, d.CreatedAtUtc);
}

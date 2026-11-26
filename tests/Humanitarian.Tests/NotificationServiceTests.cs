using FluentAssertions;
using Humanitarian.Application.Common;
using Humanitarian.Application.Notifications;
using Humanitarian.Domain.Entities;
using Humanitarian.Domain.Enums;
using Humanitarian.Tests.Support;
using Xunit;

namespace Humanitarian.Tests;

public class NotificationServiceTests
{
    private readonly InMemoryUnitOfWork _uow = new();
    private readonly FakeCurrentUser _user = new();
    private readonly RecordingAuditService _audit = new();
    private readonly NotificationService _sut;
    private static readonly Dictionary<string, string> Args = new() { ["ref"] = "ab12cd34" };

    public NotificationServiceTests()
    {
        _sut = new NotificationService(_uow, _audit, _user, TimeProvider.System);
        AsCoordinator();
    }

    private void AsCoordinator() => _user.Become(Guid.NewGuid(), "coordinator@demo.org", Roles.EmergencyCoordinator);
    private void AsCaseManager() => _user.Become(Guid.NewGuid(), "casemanager@demo.org", Roles.CaseManager);
    private void As(User u) => _user.Become(u.Id, u.Email, u.Role.ToString());

    private User AddUser(string language = "en", string? phone = null, bool active = true, UserRole role = UserRole.Citizen)
    {
        var u = new User
        {
            Email = $"{Guid.NewGuid():N}@mail.org", FullName = "Test Person", Phone = phone, PreferredLanguage = language,
            Role = role, IsActive = active
        };
        _uow.Repo<User>().Items.Add(u);
        return u;
    }

    private static NotificationRequest Req(User u, NotificationType type = NotificationType.RequestTriaged, string key = "evt-1") =>
        new(u.Id, type, key, Args, "AssistanceRequest", Guid.NewGuid().ToString());

    // ---------- staging ----------
    [Fact]
    public async Task Enqueue_CreatesTheNotification_AndAnEmailDelivery()
    {
        var user = AddUser();

        var staged = await _sut.EnqueueAsync(Req(user));

        staged.Should().BeTrue();
        var n = _uow.Repo<Notification>().Items.Should().ContainSingle().Which;
        n.RecipientUserId.Should().Be(user.Id);
        n.Title.Should().NotBeNullOrWhiteSpace();
        n.Body.Should().Contain("ab12cd34");
        var d = _uow.Repo<NotificationDelivery>().Items.Should().ContainSingle().Which;
        d.Channel.Should().Be(NotificationChannel.Email);
        d.Destination.Should().Be(user.Email);
        d.Status.Should().Be(DeliveryStatus.Pending);
    }

    [Fact]
    public async Task TheSameEvent_NeverNotifiesTheSamePersonTwice()
    {
        var user = AddUser();

        (await _sut.EnqueueAsync(Req(user, key: "request-status:42"))).Should().BeTrue();
        (await _sut.EnqueueAsync(Req(user, key: "request-status:42"))).Should().BeFalse();

        _uow.Repo<Notification>().Items.Should().HaveCount(1);
        _uow.Repo<NotificationDelivery>().Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task TheSameEvent_CanNotifyDifferentPeople()
    {
        var a = AddUser();
        var b = AddUser();

        await _sut.EnqueueAsync(Req(a, key: "shared"));
        await _sut.EnqueueAsync(Req(b, key: "shared"));

        _uow.Repo<Notification>().Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task TheMessageIsWrittenInThePersonsLanguage()
    {
        var sinhala = AddUser(language: "si");
        var english = AddUser(language: "en");

        await _sut.EnqueueAsync(Req(sinhala, key: "k1"));
        await _sut.EnqueueAsync(Req(english, key: "k2"));

        var items = _uow.Repo<Notification>().Items;
        items.Single(n => n.RecipientUserId == sinhala.Id).Language.Should().Be("si");
        items.Single(n => n.RecipientUserId == sinhala.Id).Title
            .Should().NotBe(items.Single(n => n.RecipientUserId == english.Id).Title);
    }

    [Fact]
    public async Task Sms_IsOnlyUsedForUrgentTypes_AndOnlyWithAPhoneNumber()
    {
        var withPhone = AddUser(phone: "0771234567");
        var withoutPhone = AddUser();

        await _sut.EnqueueAsync(Req(withPhone, NotificationType.RequestTriaged, "a"));      // routine: e-mail only
        await _sut.EnqueueAsync(Req(withPhone, NotificationType.ShelterPlacement, "b"));    // urgent: e-mail + SMS
        await _sut.EnqueueAsync(Req(withoutPhone, NotificationType.ShelterPlacement, "c")); // no phone: e-mail only

        var deliveries = _uow.Repo<NotificationDelivery>().Items;
        deliveries.Count(d => d.Channel == NotificationChannel.Sms).Should().Be(1);
        deliveries.Count(d => d.Channel == NotificationChannel.Email).Should().Be(3);
    }

    [Fact]
    public async Task InactiveOrUnknownRecipients_AreSkippedWithoutError()
    {
        var inactive = AddUser(active: false);

        (await _sut.EnqueueAsync(Req(inactive))).Should().BeFalse();
        (await _sut.EnqueueAsync(new NotificationRequest(Guid.NewGuid(), NotificationType.RequestClosed, "x", Args))).Should().BeFalse();

        _uow.Repo<Notification>().Items.Should().BeEmpty();
    }

    // ---------- inbox ----------
    [Fact]
    public async Task Inbox_ShowsOnlyMyNotifications_NewestFirst_WithUnreadCount()
    {
        var me = AddUser();
        var other = AddUser();
        await _sut.EnqueueAsync(Req(me, key: "1"));
        await _sut.EnqueueAsync(Req(me, key: "2"));
        await _sut.EnqueueAsync(Req(other, key: "3"));
        _uow.Repo<Notification>().Items.First(n => n.DedupeKey == "1").CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5);
        As(me);

        var page = await _sut.ListMineAsync(false, 1, 20);

        page.Total.Should().Be(2);
        page.UnreadCount.Should().Be(2);
        page.Items.Should().HaveCount(2);
        page.Items[0].CreatedAtUtc.Should().BeOnOrAfter(page.Items[1].CreatedAtUtc);
    }

    [Fact]
    public async Task MarkRead_UpdatesTheCount_AndOthersCannotMarkMyNotification()
    {
        var me = AddUser();
        var other = AddUser();
        await _sut.EnqueueAsync(Req(me));
        var notification = _uow.Repo<Notification>().Items.Single();

        As(other);
        Func<Task> act = () => _sut.MarkReadAsync(notification.Id);
        await act.Should().ThrowAsync<NotFoundException>();

        As(me);
        await _sut.MarkReadAsync(notification.Id);
        (await _sut.UnreadCountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task MarkAllRead_ReturnsHowManyWereUnread()
    {
        var me = AddUser();
        await _sut.EnqueueAsync(Req(me, key: "1"));
        await _sut.EnqueueAsync(Req(me, key: "2"));
        As(me);

        (await _sut.MarkAllReadAsync()).Should().Be(2);
        (await _sut.MarkAllReadAsync()).Should().Be(0);
    }

    // ---------- announcements ----------
    [Fact]
    public async Task Announcement_ReachesEveryActiveCitizen_AndIsAudited()
    {
        AddUser();
        AddUser();
        AddUser(active: false);
        AddUser(role: UserRole.CaseManager);

        var result = await _sut.AnnounceAsync(new AnnouncementDto(null, "Flood warning", "Move to higher ground now."));

        result.Recipients.Should().Be(2);
        result.Queued.Should().Be(2);
        _uow.Repo<Notification>().Items.Should().OnlyContain(n => n.Type == NotificationType.Announcement && n.Body == "Move to higher ground now.");
        _audit.Entries.Should().Contain(e => e.Action == "AnnouncementSent");
    }

    [Fact]
    public async Task Announcement_ForACrisis_ReachesOnlyThePeopleWhoAskedForHelpThere()
    {
        var crisis = new CrisisEvent { Name = "Flood" };
        _uow.Repo<CrisisEvent>().Items.Add(crisis);
        var inCrisis = AddUser();
        AddUser();
        _uow.Repo<AssistanceRequest>().Items.Add(new AssistanceRequest { CrisisEventId = crisis.Id, RequesterId = inCrisis.Id });

        var result = await _sut.AnnounceAsync(new AnnouncementDto(crisis.Id, "Update", "Roads reopen at noon."));

        result.Recipients.Should().Be(1);
        _uow.Repo<Notification>().Items.Single().RecipientUserId.Should().Be(inCrisis.Id);
    }

    [Fact]
    public async Task Announcement_RequiresACoordinatorOrAdministrator()
    {
        AsCaseManager();
        Func<Task> act = () => _sut.AnnounceAsync(new AnnouncementDto(null, "Hello there", "Some message"));
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Theory]
    [InlineData("ab", "A valid message")]
    [InlineData("A valid title", "")]
    public async Task Announcement_RejectsAMissingTitleOrMessage(string title, string message)
    {
        Func<Task> act = () => _sut.AnnounceAsync(new AnnouncementDto(null, title, message));
        await act.Should().ThrowAsync<AppValidationException>();
    }

    // ---------- operations ----------
    [Fact]
    public async Task OnlyFailedDeliveries_CanBeRetried_AndTheyGoBackToPending()
    {
        var failed = new NotificationDelivery { Status = DeliveryStatus.Failed, Attempts = 5, LastError = "boom", Destination = "a@b.co" };
        var sent = new NotificationDelivery { Status = DeliveryStatus.Sent, Destination = "a@b.co" };
        _uow.Repo<NotificationDelivery>().Items.AddRange([failed, sent]);

        var dto = await _sut.RetryDeliveryAsync(failed.Id);

        dto.Status.Should().Be("Pending");
        failed.Attempts.Should().Be(0);
        Func<Task> act = () => _sut.RetryDeliveryAsync(sent.Id);
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task DeliveryDestinations_AreMaskedOnTheOperationsScreen()
    {
        _uow.Repo<NotificationDelivery>().Items.Add(new NotificationDelivery { Destination = "amal.perera@mail.org", Channel = NotificationChannel.Email });
        _uow.Repo<NotificationDelivery>().Items.Add(new NotificationDelivery { Destination = "0771234567", Channel = NotificationChannel.Sms });

        var list = await _sut.ListDeliveriesAsync(null, 50);

        list.Select(d => d.Destination).Should().Contain("a***@mail.org").And.Contain("******4567");
    }

    [Fact]
    public async Task OperationsScreens_AreForCoordinatorsAndAdministratorsOnly()
    {
        AsCaseManager();
        Func<Task> list = () => _sut.ListDeliveriesAsync(null, 10);
        Func<Task> retry = () => _sut.RetryDeliveryAsync(Guid.NewGuid());
        await list.Should().ThrowAsync<ForbiddenException>();
        await retry.Should().ThrowAsync<ForbiddenException>();
    }
}

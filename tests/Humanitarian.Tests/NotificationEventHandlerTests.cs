using FluentAssertions;
using Humanitarian.Application.Common;
using Humanitarian.Application.Notifications;
using Humanitarian.Domain.Entities;
using Humanitarian.Domain.Enums;
using Humanitarian.Tests.Support;
using Xunit;

namespace Humanitarian.Tests;

public class NotificationEventHandlerTests
{
    private readonly InMemoryUnitOfWork _uow = new();
    private readonly NotificationEventHandler _sut;
    private readonly User _citizen = new() { Email = "citizen@demo.org", FullName = "Demo Citizen", PreferredLanguage = "en", Role = UserRole.Citizen, Phone = "0771234567" };

    public NotificationEventHandlerTests()
    {
        _uow.Repo<User>().Items.Add(_citizen);
        var service = new NotificationService(_uow, new RecordingAuditService(), new FakeCurrentUser(), TimeProvider.System);
        _sut = new NotificationEventHandler(service);
    }

    private RequestStatusChanged Changed(RequestStatus? from, RequestStatus to, Guid? eventId = null) =>
        new(eventId ?? Guid.NewGuid(), Guid.NewGuid(), _citizen.Id, from, to);

    [Theory]
    [InlineData(RequestStatus.Submitted, RequestStatus.UnderReview, NotificationType.RequestReceived)]
    [InlineData(RequestStatus.UnderReview, RequestStatus.Triaged, NotificationType.RequestTriaged)]
    [InlineData(RequestStatus.UnderReview, RequestStatus.Escalated, NotificationType.RequestEscalated)]
    [InlineData(RequestStatus.UnderReview, RequestStatus.Merged, NotificationType.RequestMerged)]
    [InlineData(RequestStatus.Triaged, RequestStatus.Closed, NotificationType.RequestClosed)]
    public async Task EachStatusChange_ProducesTheRightNotification(RequestStatus from, RequestStatus to, NotificationType expected)
    {
        await _sut.HandleAsync(Changed(from, to));

        _uow.Repo<Notification>().Items.Should().ContainSingle().Which.Type.Should().Be(expected);
    }

    [Fact]
    public async Task InternalSteps_AreNotWorthTellingThePersonAbout()
    {
        await _sut.HandleAsync(Changed(RequestStatus.Triaged, RequestStatus.UnderReview)); // not the first review
        await _sut.HandleAsync(Changed(null, RequestStatus.Submitted));

        _uow.Repo<Notification>().Items.Should().BeEmpty();
    }

    [Fact]
    public async Task TheSameEventProcessedTwice_NotifiesOnce()
    {
        var evt = Changed(RequestStatus.UnderReview, RequestStatus.Triaged);

        await _sut.HandleAsync(evt);
        await _sut.HandleAsync(evt);   // e.g. a replay after a crash

        _uow.Repo<Notification>().Items.Should().HaveCount(1);
        _uow.Repo<NotificationDelivery>().Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task TwoDifferentTransitionsToTheSameStatus_BothNotify()
    {
        await _sut.HandleAsync(Changed(RequestStatus.UnderReview, RequestStatus.Escalated));
        await _sut.HandleAsync(Changed(RequestStatus.Triaged, RequestStatus.Escalated));

        _uow.Repo<Notification>().Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task ShelterPlacement_TellsThePersonWhereToGo_ByEmailAndSms()
    {
        var evt = new ShelterPlacementMade(Guid.NewGuid(), Guid.NewGuid(), _citizen.Id, "Temple hall", "Kelaniya", 4);

        await _sut.HandleAsync(evt);

        var n = _uow.Repo<Notification>().Items.Should().ContainSingle().Which;
        n.Type.Should().Be(NotificationType.ShelterPlacement);
        n.Body.Should().Contain("Temple hall").And.Contain("Kelaniya");
        _uow.Repo<NotificationDelivery>().Items.Select(d => d.Channel)
            .Should().BeEquivalentTo(new[] { NotificationChannel.Email, NotificationChannel.Sms });
    }

    [Fact]
    public async Task VolunteerAssignment_NotifiesTheVolunteersAccount_WithTheShiftTimes()
    {
        var start = new DateTime(2026, 11, 25, 9, 0, 0, DateTimeKind.Utc);
        var evt = new VolunteerAssigned(Guid.NewGuid(), _citizen.Id, Guid.NewGuid(), "Deliver food packs", start, start.AddHours(4));

        await _sut.HandleAsync(evt);

        var n = _uow.Repo<Notification>().Items.Should().ContainSingle().Which;
        n.Body.Should().Contain("Deliver food packs").And.Contain("2026-11-25 09:00 UTC").And.Contain("2026-11-25 13:00 UTC");
    }
}

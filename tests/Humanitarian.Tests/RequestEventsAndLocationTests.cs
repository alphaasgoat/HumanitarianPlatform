using FluentAssertions;
using Humanitarian.Application.Common;
using Humanitarian.Application.Mapping;
using Humanitarian.Application.Requests;
using Humanitarian.Application.Requests.Ai;
using Humanitarian.Application.Requests.Duplicates;
using Humanitarian.Domain.Entities;
using Humanitarian.Domain.Enums;
using Humanitarian.Tests.Support;
using Moq;
using Xunit;

namespace Humanitarian.Tests;

/// <summary>The request workflow publishes domain events and captures a location from the address.</summary>
public class RequestEventsAndLocationTests
{
    private readonly InMemoryUnitOfWork _uow = new();
    private readonly FakeCurrentUser _user = new();
    private readonly RecordingAuditService _audit = new();
    private readonly RecordingPublisher _events = new();
    private readonly Mock<IMappingService> _mapping = new();
    private readonly AssistanceRequestService _sut;
    private readonly CrisisEvent _crisis = new() { Name = "Kelani Flood", Country = "LK", Status = CrisisStatus.Active };
    private static readonly Guid Citizen = Guid.NewGuid();

    public RequestEventsAndLocationTests()
    {
        _uow.Repo<CrisisEvent>().Items.Add(_crisis);
        var analyzer = new ResilientRequestAnalyzer(new IAnalysisStrategy[] { new RuleBasedAnalysisStrategy() });
        _sut = new AssistanceRequestService(_uow, _audit, _user, TimeProvider.System, analyzer, new DuplicateDetector(),
            _mapping.Object, _events);
    }

    private sealed class RecordingPublisher : IDomainEventPublisher
    {
        public List<IDomainEvent> Published { get; } = new();

        public Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct = default) where TEvent : IDomainEvent
        {
            Published.Add(domainEvent);
            return Task.CompletedTask;
        }
    }

    private void AsCitizen() => _user.Become(Citizen, "citizen@demo.org", Roles.Citizen);
    private void AsCoordinator() => _user.Become(Guid.NewGuid(), "coordinator@demo.org", Roles.EmergencyCoordinator);

    private SubmitRequestDto Dto(string? address = "12 Lake Road, Kelaniya", double? lat = null, double? lng = null) =>
        new(_crisis.Id, RequestCategory.Food, "We need drinking water and food for our family.", 4, UrgencyLevel.Medium,
            lat, lng, address, null);

    // ---------- events ----------
    [Fact]
    public async Task Submitting_PublishesARequestReceivedEvent_ForTheRequester()
    {
        AsCitizen();
        _mapping.Setup(m => m.GeocodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeocodeResultDto(false, false, null, null, null, "unavailable", "down"));

        var result = await _sut.SubmitAsync(Dto());

        var evt = _events.Published.Should().ContainSingle().Which.Should().BeOfType<RequestStatusChanged>().Which;
        evt.RequestId.Should().Be(result.Detail.Request.Id);
        evt.RequesterId.Should().Be(Citizen);
        evt.From.Should().Be(RequestStatus.Submitted);
        evt.To.Should().Be(RequestStatus.UnderReview);
    }

    [Fact]
    public async Task ApprovingARecommendation_PublishesATriagedEvent()
    {
        AsCitizen();
        _mapping.Setup(m => m.GeocodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeocodeResultDto(false, false, null, null, null, "unavailable", "down"));
        var created = await _sut.SubmitAsync(Dto());
        _events.Published.Clear();

        AsCoordinator();
        await _sut.ReviewAsync(created.Detail.Request.Id, new ReviewRequestDto(ReviewAction.Approve, null, null, null));

        _events.Published.Should().ContainSingle().Which.Should().BeOfType<RequestStatusChanged>()
            .Which.To.Should().Be(RequestStatus.Triaged);
    }

    [Fact]
    public async Task DecidingToDefer_ChangesNothingForThePerson_SoNothingIsPublished()
    {
        AsCitizen();
        _mapping.Setup(m => m.GeocodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeocodeResultDto(false, false, null, null, null, "unavailable", "down"));
        var created = await _sut.SubmitAsync(Dto());
        _events.Published.Clear();

        AsCoordinator();
        await _sut.ReviewAsync(created.Detail.Request.Id, new ReviewRequestDto(ReviewAction.Defer, null, null, "need more info"));

        _events.Published.Should().BeEmpty();
    }

    // ---------- location capture ----------
    [Fact]
    public async Task AnAddressWithoutCoordinates_IsGeocoded_AndTheCoordinatesAreStored()
    {
        AsCitizen();
        _mapping.Setup(m => m.GeocodeAsync("12 Lake Road, Kelaniya", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeocodeResultDto(true, true, 6.95, 79.92, "Kelaniya", "nominatim", "Location found."));

        var result = await _sut.SubmitAsync(Dto());

        var stored = _uow.Repo<AssistanceRequest>().Items.Single();
        stored.Latitude.Should().Be(6.95);
        stored.Longitude.Should().Be(79.92);
        result.Detail.Request.Latitude.Should().Be(6.95);
    }

    [Fact]
    public async Task WhenTheMapServiceIsDown_TheRequestIsStillAccepted_WithoutCoordinates()
    {
        AsCitizen();
        _mapping.Setup(m => m.GeocodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeocodeResultDto(false, false, null, null, null, "unavailable", "down"));

        var result = await _sut.SubmitAsync(Dto());

        result.WasExisting.Should().BeFalse();
        var stored = _uow.Repo<AssistanceRequest>().Items.Single();
        stored.Latitude.Should().BeNull();
        _uow.Repo<RequestHistory>().Items.Should().Contain(h => h.Action == "LocationChecked");
    }

    [Fact]
    public async Task CoordinatesGivenByThePerson_AreNeverOverwrittenByTheGeocoder()
    {
        AsCitizen();

        await _sut.SubmitAsync(Dto(lat: 7.1, lng: 80.1));

        _uow.Repo<AssistanceRequest>().Items.Single().Latitude.Should().Be(7.1);
        _mapping.Verify(m => m.GeocodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

using FluentAssertions;
using Humanitarian.Application.Common;
using Humanitarian.Application.Shelters;
using Humanitarian.Domain.Entities;
using Humanitarian.Domain.Enums;
using Humanitarian.Tests.Support;
using Xunit;

namespace Humanitarian.Tests;

public class ShelterServiceTests
{
    private readonly InMemoryUnitOfWork _uow = new();
    private readonly FakeCurrentUser _user = new();
    private readonly RecordingAuditService _audit = new();
    private readonly ShelterService _sut;
    private readonly CrisisEvent _crisis = new() { Name = "Kelani Flood", Country = "LK" };

    public ShelterServiceTests()
    {
        _uow.Repo<CrisisEvent>().Items.Add(_crisis);
        _sut = new ShelterService(_uow, _audit, _user, TimeProvider.System, new ShelterMatcher());
        AsShelterAdmin();
    }

    // ---------- helpers ----------
    private void AsShelterAdmin() => _user.Become(Guid.NewGuid(), "shelter@demo.org", Roles.ShelterAdministrator);
    private void AsCaseManager() => _user.Become(Guid.NewGuid(), "casemanager@demo.org", Roles.CaseManager);
    private void AsLogistics() => _user.Become(Guid.NewGuid(), "logistics@demo.org", Roles.LogisticsManager);

    private Shelter AddShelter(
        string name = "Temple hall", int capacity = 50, int occupancy = 0, bool accessible = true, bool medical = false,
        ShelterStatus status = ShelterStatus.Open, bool isPublic = true, double lat = 6.95, double lon = 79.92)
    {
        var s = new Shelter
        {
            Name = name, Address = "Kelaniya", Capacity = capacity, Occupancy = occupancy, WheelchairAccessible = accessible,
            HasMedicalSupport = medical, ProvidesFood = true, ProvidesWater = true, Status = status, IsPublic = isPublic,
            Latitude = lat, Longitude = lon
        };
        _uow.Repo<Shelter>().Items.Add(s);
        return s;
    }

    private AssistanceRequest AddRequest(
        int people = 4, RequestStatus status = RequestStatus.Triaged, string description = "We need a safe place to stay.")
    {
        var r = new AssistanceRequest
        {
            CrisisEventId = _crisis.Id, RequesterId = Guid.NewGuid(), Category = RequestCategory.Shelter,
            Description = description, PeopleAffected = people, DeclaredUrgency = UrgencyLevel.High, Urgency = UrgencyLevel.High,
            Status = status, Latitude = 6.95, Longitude = 79.92, CreatedAtUtc = DateTime.UtcNow
        };
        _uow.Repo<AssistanceRequest>().Items.Add(r);
        return r;
    }

    private static AllocateShelterDto Place(AssistanceRequest r, Shelter s, int? people = null) =>
        new(r.Id, s.Id, people, null);

    // ---------- shelters ----------
    [Fact]
    public async Task Create_StartsEmpty_AndIsAudited()
    {
        var dto = await _sut.CreateAsync(new CreateShelterDto(
            "Town hall", "Gampaha", 7.09, 80.0, 120, true, true, true, true, true, null, null));

        dto.Occupancy.Should().Be(0);
        dto.AvailableSpaces.Should().Be(120);
        dto.IsFull.Should().BeFalse();
        _audit.Entries.Should().Contain(e => e.Action == "ShelterCreated");
    }

    [Fact]
    public async Task Create_AsCitizen_IsForbidden()
    {
        _user.Become(Guid.NewGuid(), "citizen@demo.org", Roles.Citizen);

        await Assert.ThrowsAsync<ForbiddenException>(() => _sut.CreateAsync(new CreateShelterDto(
            "Town hall", "Gampaha", null, null, 10, false, false, false, false, true, null, null)));
    }

    [Fact]
    public async Task PublicList_HidesClosedAndNonPublicShelters_AndShowsOnlyCoarseAvailability()
    {
        AddShelter("Roomy", capacity: 100, occupancy: 10);
        AddShelter("Nearly full", capacity: 100, occupancy: 90);
        AddShelter("Full", capacity: 100, occupancy: 100);
        AddShelter("Closed", status: ShelterStatus.Closed);
        AddShelter("Private", isPublic: false);
        _user.Become(Guid.NewGuid(), "citizen@demo.org", Roles.Citizen);

        var list = await _sut.ListPublicAsync(null);

        list.Select(s => s.Name).Should().BeEquivalentTo(new[] { "Roomy", "Nearly full", "Full" });
        list.Single(s => s.Name == "Roomy").Availability.Should().Be("Available");
        list.Single(s => s.Name == "Nearly full").Availability.Should().Be("Limited");
        list.Single(s => s.Name == "Full").Availability.Should().Be("Full");
    }

    [Fact]
    public async Task Update_CapacityBelowOccupancy_IsRejected()
    {
        var shelter = AddShelter(capacity: 50, occupancy: 30);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.UpdateAsync(shelter.Id, new UpdateShelterDto(20, false, true, true, true, true, null)));
        shelter.Capacity.Should().Be(50);
    }

    [Fact]
    public async Task Close_ShelterWithPeople_IsRejected_UntilTheyAreReleased()
    {
        var shelter = AddShelter();
        var request = AddRequest(people: 4);
        var placement = await _sut.AllocateAsync(Place(request, shelter));

        await Assert.ThrowsAsync<ConflictException>(() => _sut.SetStatusAsync(shelter.Id, ShelterStatus.Closed));

        await _sut.ReleaseAsync(placement.Id, new ReleaseShelterDto("Family returned home"));
        var closed = await _sut.SetStatusAsync(shelter.Id, ShelterStatus.Closed);
        closed.Status.Should().Be("Closed");
    }

    // ---------- placement ----------
    [Fact]
    public async Task Allocate_IncreasesOccupancy_AndWritesHistoryAndAudit()
    {
        var shelter = AddShelter(capacity: 50);
        var request = AddRequest(people: 4);

        var dto = await _sut.AllocateAsync(Place(request, shelter));

        dto.Status.Should().Be("Active");
        dto.PeopleCount.Should().Be(4);
        shelter.Occupancy.Should().Be(4);
        _uow.Repo<RequestHistory>().Items.Should().Contain(h => h.Action == "ShelterAllocated" && h.IsPublic);
        _audit.Entries.Should().Contain(e => e.Action == "ShelterAllocated");
    }

    [Fact]
    public async Task Allocate_BeyondCapacity_IsRejected_AndOccupancyIsUnchanged()
    {
        var shelter = AddShelter(capacity: 3);
        var request = AddRequest(people: 4);

        await Assert.ThrowsAsync<ConflictException>(() => _sut.AllocateAsync(Place(request, shelter)));

        shelter.Occupancy.Should().Be(0);
        _uow.Repo<ShelterAllocation>().Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Allocate_SecondHouseholdThatDoesNotFit_IsRejected_NoOverAllocation()
    {
        var shelter = AddShelter(capacity: 6);
        var first = AddRequest(people: 4);
        var second = AddRequest(people: 4);

        await _sut.AllocateAsync(Place(first, shelter));
        await Assert.ThrowsAsync<ConflictException>(() => _sut.AllocateAsync(Place(second, shelter)));

        shelter.Occupancy.Should().Be(4);
        shelter.Occupancy.Should().BeLessOrEqualTo(shelter.Capacity);
    }

    [Fact]
    public async Task Allocate_TheSameRequestTwice_IsRejected()
    {
        var shelter = AddShelter();
        var request = AddRequest(people: 2);
        await _sut.AllocateAsync(Place(request, shelter));

        await Assert.ThrowsAsync<ConflictException>(() => _sut.AllocateAsync(Place(request, shelter)));
        shelter.Occupancy.Should().Be(2);
    }

    [Fact]
    public async Task Allocate_RequestThatIsNotTriaged_IsRejected()
    {
        var shelter = AddShelter();
        var request = AddRequest(status: RequestStatus.UnderReview);

        await Assert.ThrowsAsync<ConflictException>(() => _sut.AllocateAsync(Place(request, shelter)));
    }

    [Fact]
    public async Task Allocate_MorePeopleThanTheHousehold_IsRejected()
    {
        var shelter = AddShelter();
        var request = AddRequest(people: 4);

        await Assert.ThrowsAsync<AppValidationException>(() => _sut.AllocateAsync(Place(request, shelter, people: 10)));
    }

    [Fact]
    public async Task Allocate_WheelchairUserToInaccessibleShelter_IsAllowedButWarned()
    {
        var shelter = AddShelter(accessible: false);
        var request = AddRequest(people: 2, description: "My mother uses a wheelchair.");

        var dto = await _sut.AllocateAsync(Place(request, shelter));

        dto.Warnings.Should().ContainSingle().Which.Should().Contain("wheelchair");
    }

    [Fact]
    public async Task Allocate_RecordsWhenTheHumanOverridesTheTopRecommendation()
    {
        var near = AddShelter("Near hall", capacity: 10, occupancy: 7, lat: 6.95, lon: 79.92);
        var roomy = AddShelter("Roomy hall", capacity: 200, occupancy: 100, lat: 7.13, lon: 79.92);
        var request = AddRequest(people: 3);

        var recommendation = await _sut.RecommendAsync(request.Id);
        recommendation.Candidates[0].ShelterId.Should().Be(roomy.Id);

        await _sut.AllocateAsync(Place(request, near));

        _audit.Entries.Single(e => e.Action == "ShelterAllocated").Details.Should().Contain("\"humanChoiceRank\":2");
    }

    [Fact]
    public async Task Allocate_AsCaseManager_IsForbidden_ButTheyCanSeeRecommendations()
    {
        var shelter = AddShelter();
        var request = AddRequest();
        AsCaseManager();

        await Assert.ThrowsAsync<ForbiddenException>(() => _sut.AllocateAsync(Place(request, shelter)));
        (await _sut.RecommendAsync(request.Id)).Candidates.Should().ContainSingle();
    }

    [Fact]
    public async Task Recommend_WhenNothingFits_SuggestsMoreCapacity()
    {
        AddShelter(capacity: 3);
        var request = AddRequest(people: 4);

        var result = await _sut.RecommendAsync(request.Id);

        result.Candidates.Should().BeEmpty();
        result.Message.Should().Contain("partner");
    }

    // ---------- release ----------
    [Fact]
    public async Task Release_FreesTheSpaces_AndCannotBeRepeated()
    {
        var shelter = AddShelter();
        var request = AddRequest(people: 4);
        var placement = await _sut.AllocateAsync(Place(request, shelter));

        var released = await _sut.ReleaseAsync(placement.Id, new ReleaseShelterDto("Family moved to relatives"));

        released.Status.Should().Be("CheckedOut");
        shelter.Occupancy.Should().Be(0);
        await Assert.ThrowsAsync<ConflictException>(() => _sut.ReleaseAsync(placement.Id, new ReleaseShelterDto("again")));
    }

    // ---------- supply requests ----------
    [Fact]
    public async Task SupplyRequest_FlowsFromShelterToLogistics_AndCanOnlyBeResolvedOnce()
    {
        var shelter = AddShelter();
        var created = await _sut.RequestSuppliesAsync(shelter.Id, new CreateSupplyRequestDto(ResourceCategory.Water, 200, "Tanks are empty"));
        created.Status.Should().Be("Open");

        AsLogistics();
        (await _sut.ListSupplyRequestsAsync(SupplyRequestStatus.Open)).Should().ContainSingle();
        var resolved = await _sut.ResolveSupplyRequestAsync(created.Id, new ResolveSupplyRequestDto("Truck dispatched"));
        resolved.Status.Should().Be("Fulfilled");

        await Assert.ThrowsAsync<ConflictException>(() => _sut.ResolveSupplyRequestAsync(created.Id, new ResolveSupplyRequestDto(null)));
    }

    [Fact]
    public async Task SupplyRequests_AreNotVisibleToCaseManagers()
    {
        AsCaseManager();

        await Assert.ThrowsAsync<ForbiddenException>(() => _sut.ListSupplyRequestsAsync(null));
    }
}

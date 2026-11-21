using FluentAssertions;
using Humanitarian.Application.Common;
using Humanitarian.Application.Resources;
using Humanitarian.Domain.Entities;
using Humanitarian.Domain.Enums;
using Humanitarian.Tests.Support;
using Xunit;

namespace Humanitarian.Tests;

public class AllocationServiceTests
{
    private readonly InMemoryUnitOfWork _uow = new();
    private readonly FakeCurrentUser _user = new();
    private readonly RecordingAuditService _audit = new();
    private readonly AllocationService _sut;
    private readonly CrisisEvent _crisis = new() { Name = "Kelani Flood", Country = "LK" };

    public AllocationServiceTests()
    {
        _uow.Repo<CrisisEvent>().Items.Add(_crisis);
        var matcher = new ResourceMatcher();
        _sut = new AllocationService(_uow, _audit, _user, TimeProvider.System, matcher, new ShortagePlanner(matcher));
        AsLogistics();
    }

    // ---------- helpers ----------
    private void AsLogistics() => _user.Become(Guid.NewGuid(), "logistics@demo.org", Roles.LogisticsManager);
    private void AsCoordinator() => _user.Become(Guid.NewGuid(), "coordinator@demo.org", Roles.EmergencyCoordinator);

    private AssistanceRequest AddRequest(
        UrgencyLevel urgency = UrgencyLevel.High, int people = 3, RequestStatus status = RequestStatus.Triaged,
        int priority = 70, RequestCategory category = RequestCategory.Food)
    {
        var r = new AssistanceRequest
        {
            CrisisEventId = _crisis.Id, RequesterId = Guid.NewGuid(), Category = category,
            Description = "We need supplies for our family.", PeopleAffected = people,
            DeclaredUrgency = urgency, Urgency = urgency, Status = status, PriorityScore = priority,
            Latitude = 6.95, Longitude = 79.92, CreatedAtUtc = DateTime.UtcNow
        };
        _uow.Repo<AssistanceRequest>().Items.Add(r);
        return r;
    }

    private ResourceItem AddResource(
        int available = 100, ResourceCategory category = ResourceCategory.Food, int reorder = 10,
        double lat = 6.95, double lon = 79.92, string name = "Rice packs")
    {
        var item = new ResourceItem
        {
            Name = name, Category = category, Unit = "packs", QuantityTotal = available, QuantityAvailable = available,
            ReorderLevel = reorder, Latitude = lat, Longitude = lon
        };
        _uow.Repo<ResourceItem>().Items.Add(item);
        return item;
    }

    // ---------- manual allocation ----------
    [Fact]
    public async Task Allocate_ReservesStock_CreatesApprovedAllocation_HistoryAndAudit()
    {
        var request = AddRequest(people: 3);            // needs 9
        var rice = AddResource(100);

        var dto = await _sut.AllocateAsync(new AllocateDto(request.Id, rice.Id, 9, "Urgent family"));

        dto.Status.Should().Be("Approved");
        dto.Quantity.Should().Be(9);
        rice.QuantityAvailable.Should().Be(91);
        rice.QuantityTotal.Should().Be(100);
        _uow.Repo<ResourceAllocation>().Items.Should().ContainSingle();
        _uow.Repo<RequestHistory>().Items.Should().Contain(h => h.Action == "ResourceAllocated" && h.IsPublic);
        _audit.Entries.Should().Contain(e => e.Action == "ResourceAllocated");
    }

    [Fact]
    public async Task Allocate_ToRequestThatIsNotTriaged_IsRejected()
    {
        var request = AddRequest(status: RequestStatus.UnderReview);
        var rice = AddResource();

        await Assert.ThrowsAsync<ConflictException>(() => _sut.AllocateAsync(new AllocateDto(request.Id, rice.Id, 9, null)));
        rice.QuantityAvailable.Should().Be(100);
    }

    [Fact]
    public async Task Allocate_MoreThanAvailable_IsRejected_AndStockIsUnchanged()
    {
        var request = AddRequest(people: 3);
        var rice = AddResource(available: 5);

        await Assert.ThrowsAsync<ConflictException>(() => _sut.AllocateAsync(new AllocateDto(request.Id, rice.Id, 9, null)));

        rice.QuantityAvailable.Should().Be(5);
        _uow.Repo<ResourceAllocation>().Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Allocate_MoreThanTheEstimatedNeed_IsRejected()
    {
        var request = AddRequest(people: 3);            // needs 9
        var rice = AddResource(100);

        await Assert.ThrowsAsync<ConflictException>(() => _sut.AllocateAsync(new AllocateDto(request.Id, rice.Id, 10, null)));
    }

    [Fact]
    public async Task Allocate_WrongResourceCategory_IsRejected()
    {
        var request = AddRequest();
        var medicine = AddResource(category: ResourceCategory.Medicine, name: "First aid kits");

        await Assert.ThrowsAsync<AppValidationException>(() => _sut.AllocateAsync(new AllocateDto(request.Id, medicine.Id, 1, null)));
    }

    [Fact]
    public async Task Allocate_ForCriticalRequest_RequiresEmergencyCoordinator()
    {
        var request = AddRequest(urgency: UrgencyLevel.Critical, people: 3);
        var rice = AddResource(100);

        await Assert.ThrowsAsync<ForbiddenException>(() => _sut.AllocateAsync(new AllocateDto(request.Id, rice.Id, 9, null)));

        AsCoordinator();
        var dto = await _sut.AllocateAsync(new AllocateDto(request.Id, rice.Id, 9, null));
        dto.Status.Should().Be("Approved");
    }

    [Fact]
    public async Task Allocate_AsCaseManager_IsForbidden()
    {
        var request = AddRequest();
        var rice = AddResource();
        _user.Become(Guid.NewGuid(), "casemanager@demo.org", Roles.CaseManager);

        await Assert.ThrowsAsync<ForbiddenException>(() => _sut.AllocateAsync(new AllocateDto(request.Id, rice.Id, 9, null)));
    }

    [Fact]
    public async Task Allocate_RecordsWhenTheHumanOverridesTheTopRecommendation()
    {
        var request = AddRequest(urgency: UrgencyLevel.Medium, people: 4);   // needs 12
        var near = AddResource(available: 12, reorder: 10, lat: 6.95, lon: 79.92, name: "Near depot");
        var far = AddResource(available: 500, reorder: 50, lat: 7.40, lon: 79.92, name: "Far warehouse");

        var recommendation = await _sut.RecommendAsync(request.Id);
        recommendation.Candidates[0].ResourceItemId.Should().Be(far.Id);

        var dto = await _sut.AllocateAsync(new AllocateDto(request.Id, near.Id, 12, "Closest is faster"));

        dto.Rationale.Should().Contain("Human chose recommendation #2 of 2");
    }

    // ---------- recommendation ----------
    [Fact]
    public async Task Recommend_WithNoStock_SuggestsPartnerAssistance()
    {
        var request = AddRequest();

        var result = await _sut.RecommendAsync(request.Id);

        result.Candidates.Should().BeEmpty();
        result.Message.Should().Contain("partner");
    }

    [Fact]
    public async Task Recommend_ForShelterRequest_PointsToTheShelterModule()
    {
        var request = AddRequest(category: RequestCategory.Shelter);

        var result = await _sut.RecommendAsync(request.Id);

        result.Candidates.Should().BeEmpty();
        result.Message.Should().Contain("shelter");
    }

    // ---------- lifecycle ----------
    [Fact]
    public async Task Cancel_GivesTheStockBack_AndCannotBeRepeated()
    {
        var request = AddRequest();
        var rice = AddResource(100);
        var allocation = await _sut.AllocateAsync(new AllocateDto(request.Id, rice.Id, 9, null));
        rice.QuantityAvailable.Should().Be(91);

        var cancelled = await _sut.CancelAsync(allocation.Id, new CancelAllocationDto("Request resolved elsewhere"));

        cancelled.Status.Should().Be("Cancelled");
        rice.QuantityAvailable.Should().Be(100);
        await Assert.ThrowsAsync<ConflictException>(() => _sut.CancelAsync(allocation.Id, new CancelAllocationDto("again")));
    }

    [Fact]
    public async Task DispatchThenDeliver_ConsumesTheStock()
    {
        var request = AddRequest();
        var rice = AddResource(100);
        var allocation = await _sut.AllocateAsync(new AllocateDto(request.Id, rice.Id, 9, null));

        await _sut.DispatchAsync(allocation.Id);
        var delivered = await _sut.DeliverAsync(allocation.Id);

        delivered.Status.Should().Be("Delivered");
        rice.QuantityTotal.Should().Be(91);
        rice.QuantityAvailable.Should().Be(91);
    }

    [Fact]
    public async Task Deliver_BeforeDispatch_IsRejected()
    {
        var request = AddRequest();
        var rice = AddResource(100);
        var allocation = await _sut.AllocateAsync(new AllocateDto(request.Id, rice.Id, 9, null));

        await Assert.ThrowsAsync<ConflictException>(() => _sut.DeliverAsync(allocation.Id));
    }

    // ---------- shortage scenario ----------
    [Fact]
    public async Task ApplyPlan_AsLogisticsManager_IsForbidden()
    {
        AddResource(20);

        await Assert.ThrowsAsync<ForbiddenException>(() => _sut.ApplyPlanAsync(_crisis.Id, ResourceCategory.Food));
    }

    [Fact]
    public async Task ShortageScenario_UrgentRequestsAreServedFirst_AndStockNeverGoesNegative()
    {
        var critical = AddRequest(UrgencyLevel.Critical, priority: 90);
        var high = AddRequest(UrgencyLevel.High, priority: 70);
        var medium = AddRequest(UrgencyLevel.Medium, priority: 50);
        var rice = AddResource(20);                   // 27 needed, only 20 available
        AsCoordinator();

        var plan = await _sut.PlanAsync(_crisis.Id, ResourceCategory.Food);
        plan.HasShortage.Should().BeTrue();
        plan.Shortfall.Should().Be(7);
        rice.QuantityAvailable.Should().Be(20);       // planning changes nothing

        var result = await _sut.ApplyPlanAsync(_crisis.Id, ResourceCategory.Food);

        result.AllocationsCreated.Should().Be(3);
        rice.QuantityAvailable.Should().Be(0);
        QuantityFor(critical).Should().Be(9);
        QuantityFor(high).Should().Be(9);
        QuantityFor(medium).Should().Be(2);
        _audit.Entries.Should().Contain(e => e.Action == "ShortagePlanApplied");
    }

    [Fact]
    public async Task ApplyPlan_CalledTwice_DoesNotDuplicateAllocations()
    {
        AddRequest(UrgencyLevel.Critical, priority: 90);
        AddRequest(UrgencyLevel.High, priority: 70);
        AddRequest(UrgencyLevel.Medium, priority: 50);
        AddResource(20);
        AsCoordinator();

        await _sut.ApplyPlanAsync(_crisis.Id, ResourceCategory.Food);
        var second = await _sut.ApplyPlanAsync(_crisis.Id, ResourceCategory.Food);

        second.AllocationsCreated.Should().Be(0);
        _uow.Repo<ResourceAllocation>().Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task Plan_IgnoresRequestsThatAreNotTriaged()
    {
        AddRequest(UrgencyLevel.Critical, status: RequestStatus.UnderReview);
        AddRequest(UrgencyLevel.High);
        AddResource(100);

        var plan = await _sut.PlanAsync(_crisis.Id, ResourceCategory.Food);

        plan.Lines.Should().ContainSingle();
    }

    private int QuantityFor(AssistanceRequest request) =>
        _uow.Repo<ResourceAllocation>().Items.Where(a => a.RequestId == request.Id).Sum(a => a.Quantity);
}

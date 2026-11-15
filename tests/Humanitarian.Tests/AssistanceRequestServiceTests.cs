using FluentAssertions;
using Humanitarian.Application.Common;
using Humanitarian.Application.Requests;
using Humanitarian.Application.Requests.Ai;
using Humanitarian.Application.Requests.Duplicates;
using Humanitarian.Domain.Entities;
using Humanitarian.Domain.Enums;
using Humanitarian.Tests.Support;
using Xunit;

namespace Humanitarian.Tests;

public class AssistanceRequestServiceTests
{
    private readonly InMemoryUnitOfWork _uow = new();
    private readonly FakeCurrentUser _user = new();
    private readonly RecordingAuditService _audit = new();
    private readonly AssistanceRequestService _sut;
    private readonly CrisisEvent _crisis = new() { Name = "Kelani Flood", Country = "LK", Status = CrisisStatus.Active };

    private static readonly Guid CitizenA = Guid.NewGuid();
    private static readonly Guid CitizenB = Guid.NewGuid();
    private static readonly Guid Coordinator = Guid.NewGuid();
    private static readonly Guid CaseManager = Guid.NewGuid();

    public AssistanceRequestServiceTests()
    {
        _uow.Repo<CrisisEvent>().Items.Add(_crisis);
        var analyzer = new ResilientRequestAnalyzer(new IAnalysisStrategy[] { new RuleBasedAnalysisStrategy() });
        _sut = new AssistanceRequestService(_uow, _audit, _user, TimeProvider.System, analyzer, new DuplicateDetector());
    }

    // ---------- helpers ----------
    private void AsCitizen(Guid id) => _user.Become(id, $"{id}@mail.org", Roles.Citizen);
    private void AsCoordinator() => _user.Become(Coordinator, "coordinator@demo.org", Roles.EmergencyCoordinator);
    private void AsCaseManager() => _user.Become(CaseManager, "casemanager@demo.org", Roles.CaseManager);

    private SubmitRequestDto Dto(
        string description = "We need drinking water and food for our family near Kelaniya temple.",
        RequestCategory category = RequestCategory.Food, UrgencyLevel urgency = UrgencyLevel.Medium,
        int people = 4, string? key = null) =>
        new(_crisis.Id, category, description, people, urgency, 6.95, 79.92, "Kelaniya", key);

    private const string CriticalText = "Family trapped on the roof, water is rising, we have a baby.";

    private async Task<Guid> SubmitAsync(SubmitRequestDto dto)
    {
        var result = await _sut.SubmitAsync(dto);
        return result.Detail.Request.Id;
    }

    private static ReviewRequestDto Approve() => new(ReviewAction.Approve, null, null, null);

    // ---------- submit ----------
    [Fact]
    public async Task Submit_CreatesRequest_WithPendingRecommendation_History_AndAudit()
    {
        AsCitizen(CitizenA);

        var result = await _sut.SubmitAsync(Dto());

        result.WasExisting.Should().BeFalse();
        result.Detail.Request.Status.Should().Be("UnderReview");
        _uow.Repo<AssistanceRequest>().Items.Should().HaveCount(1);
        var rec = _uow.Repo<AiRecommendation>().Items.Single();
        rec.Status.Should().Be(RecommendationStatus.Pending);
        rec.Confidence.Should().BeInRange(0.0, 1.0);
        _uow.Repo<RequestHistory>().Items.Select(h => h.Action).Should().Contain("Submitted");
        _audit.Entries.Select(e => e.Action).Should().Contain(new[] { "RequestSubmitted", "AiRecommendationGenerated" });
    }

    [Fact]
    public async Task Submit_SameIdempotencyKey_ReturnsExistingRequest_WithoutCreatingAnother()
    {
        AsCitizen(CitizenA);

        var first = await _sut.SubmitAsync(Dto(key: "retry-1"));
        var second = await _sut.SubmitAsync(Dto(key: "retry-1"));

        second.WasExisting.Should().BeTrue();
        second.Detail.Request.Id.Should().Be(first.Detail.Request.Id);
        _uow.Repo<AssistanceRequest>().Items.Should().HaveCount(1);
        _uow.Repo<AiRecommendation>().Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Submit_ToClosedCrisis_IsRejected()
    {
        AsCitizen(CitizenA);
        _crisis.Status = CrisisStatus.Closed;

        await Assert.ThrowsAsync<ConflictException>(() => _sut.SubmitAsync(Dto()));
    }

    [Fact]
    public async Task Submit_SimilarRequestFromAnotherPerson_FlagsDuplicate_ButKeepsBothRequests()
    {
        AsCitizen(CitizenA);
        var firstId = await SubmitAsync(Dto());
        AsCitizen(CitizenB);
        var secondId = await SubmitAsync(Dto("We need drinking water and food for our family near the Kelaniya temple."));

        _uow.Repo<AssistanceRequest>().Items.Should().HaveCount(2);   // nothing deleted
        var link = _uow.Repo<DuplicateLink>().Items.Single();
        link.RequestId.Should().Be(secondId);
        link.DuplicateOfRequestId.Should().Be(firstId);
        link.Status.Should().Be(DuplicateStatus.Suspected);

        AsCoordinator();
        var detail = await _sut.GetAsync(secondId);
        detail.PossibleDuplicates.Should().HaveCount(1);
    }

    // ---------- visibility ----------
    [Fact]
    public async Task Citizen_CannotSeeAnotherCitizensRequest()
    {
        AsCitizen(CitizenA);
        var id = await SubmitAsync(Dto());

        AsCitizen(CitizenB);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetAsync(id));
    }

    [Fact]
    public async Task Citizen_SeesOwnRequest_ButNotTheAiRecommendationOrInternalNotes()
    {
        AsCitizen(CitizenA);
        var id = await SubmitAsync(Dto());

        var detail = await _sut.GetAsync(id);

        detail.LatestRecommendation.Should().BeNull();
        detail.PossibleDuplicates.Should().BeEmpty();
        detail.Request.PriorityScore.Should().Be(0);
        detail.Timeline.Select(t => t.Action).Should().NotContain("AiRecommendationGenerated");
    }

    [Fact]
    public async Task Citizen_CannotReviewRequests()
    {
        AsCitizen(CitizenA);
        var id = await SubmitAsync(Dto());

        await Assert.ThrowsAsync<ForbiddenException>(() => _sut.ReviewAsync(id, Approve()));
    }

    // ---------- human-in-the-loop ----------
    [Fact]
    public async Task Approve_RecordsHumanDecision_AndWritesAuditWithConfidence()
    {
        AsCitizen(CitizenA);
        var id = await SubmitAsync(Dto());
        AsCoordinator();

        var detail = await _sut.ReviewAsync(id, Approve());

        var rec = _uow.Repo<AiRecommendation>().Items.Single();
        rec.Status.Should().Be(RecommendationStatus.Approved);
        rec.DecidedByUserId.Should().Be(Coordinator);
        rec.DecidedAtUtc.Should().NotBeNull();
        detail.Request.Status.Should().Be("Triaged");

        var entry = _audit.Entries.Single(e => e.Action == "AiDecisionApproved");
        entry.Details.Should().Contain("confidence").And.Contain("humanAction").And.Contain("decidedAtUtc");
    }

    [Fact]
    public async Task Modify_KeepsOriginalRecommendation_AndStoresOverride()
    {
        AsCitizen(CitizenA);
        var id = await SubmitAsync(Dto(urgency: UrgencyLevel.Medium));
        AsCaseManager();
        var suggested = _uow.Repo<AiRecommendation>().Items.Single().SuggestedUrgency;

        await _sut.ReviewAsync(id, new ReviewRequestDto(ReviewAction.Modify, null, UrgencyLevel.Low, "Water is already available nearby."));

        var rec = _uow.Repo<AiRecommendation>().Items.Single();
        rec.Status.Should().Be(RecommendationStatus.Modified);
        rec.SuggestedUrgency.Should().Be(suggested);          // original preserved
        rec.FinalUrgency.Should().Be(UrgencyLevel.Low);       // human override
        rec.DecisionNote.Should().Contain("Water is already");
        _uow.Repo<AssistanceRequest>().Items.Single().Urgency.Should().Be(UrgencyLevel.Low);
        _audit.Entries.Should().Contain(e => e.Action == "AiDecisionModified");
    }

    [Fact]
    public async Task Modify_WithoutNote_IsRejected()
    {
        AsCitizen(CitizenA);
        var id = await SubmitAsync(Dto());
        AsCoordinator();

        await Assert.ThrowsAsync<AppValidationException>(() =>
            _sut.ReviewAsync(id, new ReviewRequestDto(ReviewAction.Modify, null, UrgencyLevel.Low, null)));
    }

    [Fact]
    public async Task Reject_RequiresNote_AndLeavesRequestValuesUnchanged()
    {
        AsCitizen(CitizenA);
        var id = await SubmitAsync(Dto(urgency: UrgencyLevel.Medium));
        AsCoordinator();

        await Assert.ThrowsAsync<AppValidationException>(() =>
            _sut.ReviewAsync(id, new ReviewRequestDto(ReviewAction.Reject, null, null, null)));

        await _sut.ReviewAsync(id, new ReviewRequestDto(ReviewAction.Reject, null, null, "AI misread the message."));

        _uow.Repo<AiRecommendation>().Items.Single().Status.Should().Be(RecommendationStatus.Rejected);
        _uow.Repo<AssistanceRequest>().Items.Single().Urgency.Should().Be(UrgencyLevel.Medium);
    }

    [Fact]
    public async Task Defer_KeepsRecommendationReviewableLater()
    {
        AsCitizen(CitizenA);
        var id = await SubmitAsync(Dto());
        AsCoordinator();

        await _sut.ReviewAsync(id, new ReviewRequestDto(ReviewAction.Defer, null, null, null));
        await _sut.ReviewAsync(id, Approve());   // can still be decided afterwards

        _uow.Repo<AiRecommendation>().Items.Single().Status.Should().Be(RecommendationStatus.Approved);
    }

    [Fact]
    public async Task ApprovingCriticalPriority_RequiresAnEmergencyCoordinator()
    {
        AsCitizen(CitizenA);
        var id = await SubmitAsync(Dto(CriticalText, RequestCategory.Shelter, UrgencyLevel.High, 5));
        _uow.Repo<AiRecommendation>().Items.Single().SuggestedUrgency.Should().Be(UrgencyLevel.Critical);

        AsCaseManager();
        await Assert.ThrowsAsync<ForbiddenException>(() => _sut.ReviewAsync(id, Approve()));
        _uow.Repo<AiRecommendation>().Items.Single().Status.Should().Be(RecommendationStatus.Pending); // unchanged

        AsCoordinator();
        var detail = await _sut.ReviewAsync(id, Approve());
        detail.Request.Urgency.Should().Be("Critical");
    }

    [Fact]
    public async Task EscalatedRequest_CanOnlyBeDecidedByCoordinator()
    {
        AsCitizen(CitizenA);
        var id = await SubmitAsync(Dto());
        AsCaseManager();
        await _sut.EscalateAsync(id, "Needs senior decision");

        await Assert.ThrowsAsync<ForbiddenException>(() => _sut.ReviewAsync(id, Approve()));
    }

    [Fact]
    public async Task Analyze_CreatesNewRecommendation_AndSupersedesThePendingOne()
    {
        AsCitizen(CitizenA);
        var id = await SubmitAsync(Dto());
        AsCoordinator();

        await _sut.AnalyzeAsync(id);

        var recs = _uow.Repo<AiRecommendation>().Items;
        recs.Should().HaveCount(2);
        recs.Count(r => r.Status == RecommendationStatus.Superseded).Should().Be(1);
        recs.Count(r => r.Status == RecommendationStatus.Pending).Should().Be(1);
    }

    // ---------- merge / close ----------
    [Fact]
    public async Task Merge_MarksRequestMerged_WithoutDeletingAnything()
    {
        AsCitizen(CitizenA);
        var firstId = await SubmitAsync(Dto());
        AsCitizen(CitizenB);
        var secondId = await SubmitAsync(Dto("We need drinking water and food for our family near the Kelaniya temple."));
        AsCoordinator();

        var detail = await _sut.MergeAsync(secondId, new MergeRequestDto(firstId, "Same household"));

        detail.Request.Status.Should().Be("Merged");
        detail.Request.MergedIntoRequestId.Should().Be(firstId);
        _uow.Repo<AssistanceRequest>().Items.Should().HaveCount(2);
        _uow.Repo<DuplicateLink>().Items.Single().Status.Should().Be(DuplicateStatus.ConfirmedMerged);
        _audit.Entries.Should().Contain(e => e.Action == "RequestMerged");
    }

    [Fact]
    public async Task ClosedRequest_CannotBeReviewedAgain()
    {
        AsCitizen(CitizenA);
        var id = await SubmitAsync(Dto());
        AsCoordinator();
        await _sut.CloseAsync(id, "Resolved");

        await Assert.ThrowsAsync<ConflictException>(() => _sut.ReviewAsync(id, Approve()));
    }

    [Fact]
    public async Task Queue_IsOrderedByPriority_HighestFirst()
    {
        AsCitizen(CitizenA);
        await SubmitAsync(Dto("We would like some rice and milk powder next week", RequestCategory.Food, UrgencyLevel.Low, 1));
        AsCitizen(CitizenB);
        await SubmitAsync(Dto(CriticalText, RequestCategory.Shelter, UrgencyLevel.High, 5));
        AsCoordinator();

        var page = await _sut.ListAsync(new RequestFilter(null, null, null, null), 1, 20);

        page.Total.Should().Be(2);
        page.Items[0].PriorityScore.Should().BeGreaterThan(page.Items[1].PriorityScore);
    }
}

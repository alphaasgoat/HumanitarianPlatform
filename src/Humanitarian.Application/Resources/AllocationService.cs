using System.Text.Json;
using Humanitarian.Application.Common;
using Humanitarian.Domain.Entities;
using Humanitarian.Domain.Enums;

namespace Humanitarian.Application.Resources;

public interface IAllocationService
{
    Task<MatchResultDto> RecommendAsync(Guid requestId, CancellationToken ct = default);
    Task<AllocationDto> AllocateAsync(AllocateDto dto, CancellationToken ct = default);
    Task<ShortagePlanDto> PlanAsync(Guid crisisEventId, ResourceCategory category, CancellationToken ct = default);
    Task<ApplyPlanResultDto> ApplyPlanAsync(Guid crisisEventId, ResourceCategory category, CancellationToken ct = default);
    Task<AllocationDto> DispatchAsync(Guid allocationId, CancellationToken ct = default);
    Task<AllocationDto> DeliverAsync(Guid allocationId, CancellationToken ct = default);
    Task<AllocationDto> CancelAsync(Guid allocationId, CancelAllocationDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<AllocationDto>> ListByRequestAsync(Guid requestId, CancellationToken ct = default);
    Task<IReadOnlyList<AllocationDto>> ListByCrisisAsync(Guid crisisEventId, AllocationStatus? status, CancellationToken ct = default);
}

/// <summary>
/// Turns triaged requests into reserved stock. The matcher only RECOMMENDS; a person approves every
/// allocation. Critical requests and shortage plans need an Emergency Coordinator.
/// Stock changes and the allocation row are saved in one SaveChanges (atomic), and a concurrency token
/// on the resource stops two people from reserving the same last units.
/// </summary>
public class AllocationService(
    IUnitOfWork uow, IAuditService audit, ICurrentUser current, TimeProvider time,
    IResourceMatcher matcher, IShortagePlanner planner) : IAllocationService
{
    private DateTime Now => time.GetUtcNow().UtcDateTime;
    private bool IsCoordinator => current.Role == Roles.EmergencyCoordinator;
    private bool CanManage => current.Role is Roles.LogisticsManager or Roles.EmergencyCoordinator;
    private bool CanView => CanManage || current.Role == Roles.CaseManager;

    // =====================================================================
    // Recommendation (read-only)
    // =====================================================================
    public async Task<MatchResultDto> RecommendAsync(Guid requestId, CancellationToken ct = default)
    {
        RequireView();
        var request = await LoadRequestAsync(requestId, ct);

        var category = ResourceRules.ResourceCategoryFor(request.Category);
        if (category is null)
            return new MatchResultDto(requestId, "None", 0, Array.Empty<MatchCandidateDto>(),
                "Shelter requests are handled by the shelter module, not by resource allocation.");
        var cat = category.Value;

        var needed = ResourceRules.EstimateQuantity(request) - await AlreadyAllocatedAsync(requestId, ct);
        if (needed <= 0)
            return new MatchResultDto(requestId, cat.ToString(), 0, Array.Empty<MatchCandidateDto>(),
                "This request already has enough resources allocated.");

        var resources = await uow.Repository<ResourceItem>().ListAsync(r => r.IsActive && r.Category == cat, ct: ct);
        var ranked = matcher.Rank(request, needed, resources);
        var candidates = ranked.Take(5).Select(m => new MatchCandidateDto(
            m.Resource.Id, m.Resource.Name, m.Resource.Category.ToString(), m.Resource.QuantityAvailable,
            m.DistanceKm, m.Score, m.CanFullyCover, m.Rationale)).ToList();

        string message;
        if (candidates.Count == 0)
            message = "No available resource of this category. Consider requesting assistance from a partner organisation.";
        else if (!ranked[0].CanFullyCover)
            message = "The preferred resource cannot cover the whole need. Combine it with an alternative or request partner assistance.";
        else
            message = "Preferred resource and alternatives are listed best first. A person must approve the allocation.";

        return new MatchResultDto(requestId, cat.ToString(), needed, candidates, message);
    }

    // =====================================================================
    // Manual allocation (human decision)
    // =====================================================================
    public async Task<AllocationDto> AllocateAsync(AllocateDto dto, CancellationToken ct = default)
    {
        RequireManage();
        var request = await LoadRequestAsync(dto.RequestId, ct);

        if (request.Status != RequestStatus.Triaged)
            throw new ConflictException("Resources can only be allocated to requests that a human reviewer has triaged.");
        if (request.Urgency == UrgencyLevel.Critical && !IsCoordinator)
            throw new ForbiddenException("Allocations for Critical requests require an Emergency Coordinator.");

        var wanted = ResourceRules.ResourceCategoryFor(request.Category)
                     ?? throw new AppValidationException("This request type is handled by the shelter module, not by resource allocation.");

        var resourceRepo = uow.Repository<ResourceItem>();
        var resource = await resourceRepo.GetByIdAsync(dto.ResourceItemId, ct)
                       ?? throw new NotFoundException("Resource not found.");
        if (!resource.IsActive) throw new ConflictException("This resource is not active.");
        if (resource.Category != wanted)
            throw new AppValidationException($"Resource category {resource.Category} does not match the request ({wanted}).");
        if (dto.Quantity > resource.QuantityAvailable)
            throw new ConflictException(
                $"Only {resource.QuantityAvailable} {resource.Unit} of {resource.Name} are available. Choose an alternative or request partner assistance.");

        var estimate = ResourceRules.EstimateQuantity(request);
        var already = await AlreadyAllocatedAsync(request.Id, ct);
        if (already + dto.Quantity > estimate)
            throw new ConflictException($"This request needs about {estimate}; {already} already allocated.");

        // Record how the human choice compares with the matcher's recommendation.
        var all = await resourceRepo.ListAsync(r => r.IsActive && r.Category == wanted, ct: ct);
        var ranked = matcher.Rank(request, Math.Max(1, estimate - already), all);
        var position = ranked.ToList().FindIndex(m => m.Resource.Id == resource.Id);
        var match = position >= 0 ? ranked[position] : null;
        var score = match?.Score ?? 0;
        var rationale = match is null
            ? "Manual allocation."
            : match.Rationale + (position == 0
                ? " Matches the top recommendation."
                : $" Human chose recommendation #{position + 1} of {ranked.Count}.");

        var now = Now;
        resource.QuantityAvailable -= dto.Quantity;
        resource.UpdatedAtUtc = now;
        resourceRepo.Update(resource);

        var allocation = NewAllocation(request, resource.Id, dto.Quantity, score, rationale, now);
        await uow.Repository<ResourceAllocation>().AddAsync(allocation, ct);

        await AddHistoryAsync(request.Id, "ResourceAllocated", $"{dto.Quantity} {resource.Unit} of {resource.Name}", true, ct);
        await audit.RecordAsync("ResourceAllocated", nameof(AssistanceRequest), request.Id.ToString(),
            JsonSerializer.Serialize(new
            {
                allocationId = allocation.Id,
                resourceId = resource.Id,
                quantity = dto.Quantity,
                matcherScore = score,
                recommendedFirst = ranked.Count > 0 ? ranked[0].Resource.Id : (Guid?)null,
                humanChoiceRank = position >= 0 ? position + 1 : (int?)null,
                note = dto.Note
            }), ct: ct);

        await uow.SaveChangesAsync(ct);
        return ToDto(allocation);
    }

    // =====================================================================
    // Shortage scenario: several urgent requests, limited stock
    // =====================================================================
    public async Task<ShortagePlanDto> PlanAsync(Guid crisisEventId, ResourceCategory category, CancellationToken ct = default)
    {
        RequireView();
        var ctx = await BuildPlanAsync(crisisEventId, category, ct);
        var lines = ctx.Plan.Lines.Select(l => new PlanLineDto(
            l.Request.Id, l.Request.PriorityScore, l.Request.Urgency.ToString(), l.Request.PeopleAffected,
            l.Needed, l.Proposed, l.Outcome,
            l.Takes.Select(t => new PlanTakeDto(t.ResourceItemId, t.ResourceName, t.Quantity)).ToList())).ToList();
        return new ShortagePlanDto(crisisEventId, category.ToString(), ctx.Plan.TotalNeeded, ctx.Plan.TotalAvailable,
            ctx.Plan.Shortfall, ctx.Plan.HasShortage, lines);
    }

    public async Task<ApplyPlanResultDto> ApplyPlanAsync(Guid crisisEventId, ResourceCategory category, CancellationToken ct = default)
    {
        RequireCoordinator();
        // The plan is recomputed on the server: the client can never inject its own quantities.
        var ctx = await BuildPlanAsync(crisisEventId, category, ct);
        var now = Now;
        var created = new List<ResourceAllocation>();
        var notServed = 0;

        foreach (var line in ctx.Plan.Lines)
        {
            if (line.Proposed <= 0) { notServed++; continue; }

            foreach (var take in line.Takes)
            {
                var resource = ctx.Resources[take.ResourceItemId];
                if (resource.QuantityAvailable < take.Quantity)
                    throw new ConflictException($"Stock of {resource.Name} changed while applying the plan. Generate the plan again.");

                resource.QuantityAvailable -= take.Quantity;
                resource.UpdatedAtUtc = now;
                uow.Repository<ResourceItem>().Update(resource);

                var rationale = $"Shortage plan: urgency {line.Request.Urgency}, priority score {line.Request.PriorityScore}, " +
                                $"{line.Outcome} ({line.Proposed}/{line.Needed}).";
                var allocation = NewAllocation(line.Request, resource.Id, take.Quantity, 0, rationale, now);
                await uow.Repository<ResourceAllocation>().AddAsync(allocation, ct);
                created.Add(allocation);

                await AddHistoryAsync(line.Request.Id, "ResourceAllocated",
                    $"{take.Quantity} {resource.Unit} of {resource.Name} (shortage plan, {line.Outcome})", true, ct);
                await audit.RecordAsync("ResourceAllocated", nameof(AssistanceRequest), line.Request.Id.ToString(),
                    JsonSerializer.Serialize(new { allocationId = allocation.Id, source = "ShortagePlan", resourceId = resource.Id, quantity = take.Quantity, outcome = line.Outcome }),
                    ct: ct);
            }
        }

        await audit.RecordAsync("ShortagePlanApplied", nameof(CrisisEvent), crisisEventId.ToString(),
            JsonSerializer.Serialize(new
            {
                category = category.ToString(),
                totalNeeded = ctx.Plan.TotalNeeded,
                totalAvailable = ctx.Plan.TotalAvailable,
                shortfall = ctx.Plan.Shortfall,
                allocations = created.Count,
                requestsNotServed = notServed
            }), ct: ct);

        await uow.SaveChangesAsync(ct); // one save = one transaction for every stock change and allocation
        return new ApplyPlanResultDto(created.Count, notServed, created.Select(a => ToDto(a)).ToList());
    }

    // =====================================================================
    // Lifecycle: dispatch, deliver, cancel
    // =====================================================================
    public async Task<AllocationDto> DispatchAsync(Guid allocationId, CancellationToken ct = default)
    {
        RequireManage();
        var allocation = await LoadAllocationAsync(allocationId, ct);
        if (allocation.Status != AllocationStatus.Approved)
            throw new ConflictException($"Only approved allocations can be dispatched (current status: {allocation.Status}).");

        var now = Now;
        allocation.Status = AllocationStatus.Dispatched;
        allocation.DispatchedAtUtc = now;
        allocation.UpdatedAtUtc = now;
        uow.Repository<ResourceAllocation>().Update(allocation);

        await AddHistoryAsync(allocation.RequestId, "ResourceDispatched", null, true, ct);
        await audit.RecordAsync("ResourceDispatched", nameof(ResourceAllocation), allocation.Id.ToString(), ct: ct);
        await uow.SaveChangesAsync(ct);
        return ToDto(allocation);
    }

    public async Task<AllocationDto> DeliverAsync(Guid allocationId, CancellationToken ct = default)
    {
        RequireManage();
        var allocation = await LoadAllocationAsync(allocationId, ct);
        if (allocation.Status != AllocationStatus.Dispatched)
            throw new ConflictException($"Only dispatched allocations can be marked delivered (current status: {allocation.Status}).");

        var resource = await uow.Repository<ResourceItem>().GetByIdAsync(allocation.ResourceItemId, ct)
                       ?? throw new NotFoundException("Resource not found.");
        if (resource.QuantityTotal - allocation.Quantity < resource.QuantityAvailable)
            throw new ConflictException("Inventory is inconsistent for this resource; please review the stock levels.");

        var now = Now;
        resource.QuantityTotal -= allocation.Quantity; // the units have left the depot
        resource.UpdatedAtUtc = now;
        uow.Repository<ResourceItem>().Update(resource);

        allocation.Status = AllocationStatus.Delivered;
        allocation.DeliveredAtUtc = now;
        allocation.UpdatedAtUtc = now;
        uow.Repository<ResourceAllocation>().Update(allocation);

        await AddHistoryAsync(allocation.RequestId, "ResourceDelivered", null, true, ct);
        await audit.RecordAsync("ResourceDelivered", nameof(ResourceAllocation), allocation.Id.ToString(), ct: ct);
        await uow.SaveChangesAsync(ct);
        return ToDto(allocation);
    }

    public async Task<AllocationDto> CancelAsync(Guid allocationId, CancelAllocationDto dto, CancellationToken ct = default)
    {
        RequireManage();
        var allocation = await LoadAllocationAsync(allocationId, ct);
        if (allocation.Status is AllocationStatus.Delivered or AllocationStatus.Cancelled)
            throw new ConflictException($"An allocation that is {allocation.Status} cannot be cancelled.");

        var resource = await uow.Repository<ResourceItem>().GetByIdAsync(allocation.ResourceItemId, ct)
                       ?? throw new NotFoundException("Resource not found.");

        var now = Now;
        resource.QuantityAvailable = Math.Min(resource.QuantityTotal, resource.QuantityAvailable + allocation.Quantity);
        resource.UpdatedAtUtc = now;
        uow.Repository<ResourceItem>().Update(resource);

        allocation.Status = AllocationStatus.Cancelled;
        allocation.CancelReason = dto.Reason.Trim();
        allocation.UpdatedAtUtc = now;
        uow.Repository<ResourceAllocation>().Update(allocation);

        await AddHistoryAsync(allocation.RequestId, "ResourceAllocationCancelled", dto.Reason.Trim(), false, ct);
        await audit.RecordAsync("ResourceAllocationCancelled", nameof(ResourceAllocation), allocation.Id.ToString(), dto.Reason.Trim(), ct: ct);
        await uow.SaveChangesAsync(ct);
        return ToDto(allocation);
    }

    // =====================================================================
    // Queries
    // =====================================================================
    public async Task<IReadOnlyList<AllocationDto>> ListByRequestAsync(Guid requestId, CancellationToken ct = default)
    {
        RequireView();
        var list = await uow.Repository<ResourceAllocation>().ListAsync(
            a => a.RequestId == requestId, q => q.OrderBy(a => a.ApprovedAtUtc), ct: ct);
        return list.Select(a => ToDto(a)).ToList();
    }

    public async Task<IReadOnlyList<AllocationDto>> ListByCrisisAsync(Guid crisisEventId, AllocationStatus? status, CancellationToken ct = default)
    {
        RequireView();
        var st = status;
        var list = await uow.Repository<ResourceAllocation>().ListAsync(
            a => a.CrisisEventId == crisisEventId && (st == null || a.Status == st),
            q => q.OrderByDescending(a => a.ApprovedAtUtc), ct: ct);
        return list.Select(a => ToDto(a)).ToList();
    }

    // =====================================================================
    // Helpers
    // =====================================================================
    private sealed record PlanContext(ShortagePlan Plan, Dictionary<Guid, ResourceItem> Resources);

    private async Task<PlanContext> BuildPlanAsync(Guid crisisEventId, ResourceCategory category, CancellationToken ct)
    {
        if (!await uow.Repository<CrisisEvent>().AnyAsync(c => c.Id == crisisEventId, ct))
            throw new NotFoundException("Crisis event not found.");

        // Only requests a human has triaged can receive resources.
        var triaged = await uow.Repository<AssistanceRequest>().ListAsync(
            r => r.CrisisEventId == crisisEventId && r.Status == RequestStatus.Triaged, ct: ct);
        var requests = triaged.Where(r => ResourceRules.ResourceCategoryFor(r.Category) == category).ToList();

        var resources = await uow.Repository<ResourceItem>().ListAsync(r => r.IsActive && r.Category == category, ct: ct);

        var allocations = await uow.Repository<ResourceAllocation>().ListAsync(
            a => a.CrisisEventId == crisisEventId && a.Status != AllocationStatus.Cancelled, ct: ct);
        var already = allocations.GroupBy(a => a.RequestId).ToDictionary(g => g.Key, g => g.Sum(a => a.Quantity));

        var plan = planner.Plan(requests, resources, already);
        return new PlanContext(plan, resources.ToDictionary(r => r.Id));
    }

    private async Task<int> AlreadyAllocatedAsync(Guid requestId, CancellationToken ct)
    {
        var existing = await uow.Repository<ResourceAllocation>().ListAsync(
            a => a.RequestId == requestId && a.Status != AllocationStatus.Cancelled, ct: ct);
        return existing.Sum(a => a.Quantity);
    }

    private ResourceAllocation NewAllocation(AssistanceRequest request, Guid resourceId, int quantity, double score,
        string rationale, DateTime now) => new()
    {
        CrisisEventId = request.CrisisEventId,
        RequestId = request.Id,
        ResourceItemId = resourceId,
        Quantity = quantity,
        Status = AllocationStatus.Approved,
        Score = score,
        Rationale = rationale,
        ApprovedByUserId = current.UserId ?? Guid.Empty,
        ApprovedByEmail = current.Email,
        ApprovedAtUtc = now,
        CreatedAtUtc = now
    };

    private Task AddHistoryAsync(Guid requestId, string action, string? note, bool isPublic, CancellationToken ct) =>
        uow.Repository<RequestHistory>().AddAsync(new RequestHistory
        {
            RequestId = requestId,
            Action = action,
            ActorUserId = current.UserId,
            ActorEmail = current.Email,
            Note = note,
            IsPublic = isPublic,
            CreatedAtUtc = Now
        }, ct);

    private async Task<AssistanceRequest> LoadRequestAsync(Guid id, CancellationToken ct) =>
        await uow.Repository<AssistanceRequest>().GetByIdAsync(id, ct) ?? throw new NotFoundException("Request not found.");

    private async Task<ResourceAllocation> LoadAllocationAsync(Guid id, CancellationToken ct) =>
        await uow.Repository<ResourceAllocation>().GetByIdAsync(id, ct) ?? throw new NotFoundException("Allocation not found.");

    private void RequireView()
    {
        if (!CanView) throw new ForbiddenException("You are not allowed to view resource allocations.");
    }

    private void RequireManage()
    {
        if (!CanManage) throw new ForbiddenException("Only logistics managers and emergency coordinators can manage allocations.");
    }

    private void RequireCoordinator()
    {
        if (!IsCoordinator) throw new ForbiddenException("Applying a shortage plan requires an Emergency Coordinator.");
    }

    private static AllocationDto ToDto(ResourceAllocation a) => new(
        a.Id, a.CrisisEventId, a.RequestId, a.ResourceItemId, a.Quantity, a.Status.ToString(), a.Score, a.Rationale,
        a.ApprovedByUserId, a.ApprovedByEmail, a.ApprovedAtUtc, a.DispatchedAtUtc, a.DeliveredAtUtc, a.CancelReason);
}

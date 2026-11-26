using System.Linq.Expressions;
using System.Text.Json;
using Humanitarian.Application.Common;
using Humanitarian.Domain.Entities;
using Humanitarian.Domain.Enums;

namespace Humanitarian.Application.Shelters;

public interface IShelterService
{
    Task<ShelterDto> CreateAsync(CreateShelterDto dto, CancellationToken ct = default);
    Task<ShelterDto> UpdateAsync(Guid id, UpdateShelterDto dto, CancellationToken ct = default);
    Task<ShelterDto> SetStatusAsync(Guid id, ShelterStatus status, CancellationToken ct = default);
    Task<IReadOnlyList<ShelterDto>> ListAsync(Guid? crisisEventId, CancellationToken ct = default);
    Task<ShelterDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PublicShelterDto>> ListPublicAsync(Guid? crisisEventId, CancellationToken ct = default);

    Task<ShelterRecommendationDto> RecommendAsync(Guid requestId, CancellationToken ct = default);
    Task<ShelterAllocationDto> AllocateAsync(AllocateShelterDto dto, CancellationToken ct = default);
    Task<ShelterAllocationDto> ReleaseAsync(Guid allocationId, ReleaseShelterDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<ShelterAllocationDto>> ListAllocationsAsync(Guid shelterId, bool activeOnly, CancellationToken ct = default);

    Task<SupplyRequestDto> RequestSuppliesAsync(Guid shelterId, CreateSupplyRequestDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<SupplyRequestDto>> ListSupplyRequestsAsync(SupplyRequestStatus? status, CancellationToken ct = default);
    Task<SupplyRequestDto> ResolveSupplyRequestAsync(Guid id, ResolveSupplyRequestDto dto, CancellationToken ct = default);
}

/// <summary>
/// Shelter management. The matcher only RECOMMENDS; a person places each household. Over-allocation is
/// prevented twice: a capacity check in the service and a concurrency token on the shelter row, so two
/// people cannot fill the last bed at the same moment.
/// </summary>
public class ShelterService(
    IUnitOfWork uow, IAuditService audit, ICurrentUser current, TimeProvider time, IShelterMatcher matcher,
    IDomainEventPublisher? events = null) : IShelterService
{
    // Optional collaborator: without it nothing is published (existing behaviour).
    private IDomainEventPublisher Events => events ?? NullDomainEventPublisher.Instance;

    private DateTime Now => time.GetUtcNow().UtcDateTime;
    private bool CanManage => current.Role is Roles.ShelterAdministrator or Roles.EmergencyCoordinator;
    private bool CanView => CanManage || current.Role == Roles.CaseManager;
    private bool CanSupplies => CanManage || current.Role == Roles.LogisticsManager;

    // =====================================================================
    // Shelters
    // =====================================================================
    public async Task<ShelterDto> CreateAsync(CreateShelterDto dto, CancellationToken ct = default)
    {
        RequireManage();
        if (dto.Latitude.HasValue != dto.Longitude.HasValue)
            throw new AppValidationException("Latitude and longitude must be provided together.");
        if (dto.CrisisEventId is { } cid && !await uow.Repository<CrisisEvent>().AnyAsync(c => c.Id == cid, ct))
            throw new AppValidationException("Crisis event does not exist.");

        var shelter = new Shelter
        {
            CrisisEventId = dto.CrisisEventId,
            Name = dto.Name.Trim(),
            Address = dto.Address.Trim(),
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            Capacity = dto.Capacity,
            Occupancy = 0,
            HasMedicalSupport = dto.HasMedicalSupport,
            WheelchairAccessible = dto.WheelchairAccessible,
            ProvidesFood = dto.ProvidesFood,
            ProvidesWater = dto.ProvidesWater,
            IsPublic = dto.IsPublic,
            Notes = dto.Notes,
            CreatedAtUtc = Now
        };
        await uow.Repository<Shelter>().AddAsync(shelter, ct);
        await audit.RecordAsync("ShelterCreated", nameof(Shelter), shelter.Id.ToString(), $"{shelter.Name}; capacity {shelter.Capacity}", ct: ct);
        await uow.SaveChangesAsync(ct);
        return ToDto(shelter);
    }

    public async Task<ShelterDto> UpdateAsync(Guid id, UpdateShelterDto dto, CancellationToken ct = default)
    {
        RequireManage();
        var shelter = await LoadShelterAsync(id, ct);
        if (dto.Capacity < shelter.Occupancy)
            throw new ConflictException($"Capacity cannot be lower than the current occupancy ({shelter.Occupancy}).");

        shelter.Capacity = dto.Capacity;
        shelter.HasMedicalSupport = dto.HasMedicalSupport;
        shelter.WheelchairAccessible = dto.WheelchairAccessible;
        shelter.ProvidesFood = dto.ProvidesFood;
        shelter.ProvidesWater = dto.ProvidesWater;
        shelter.IsPublic = dto.IsPublic;
        shelter.Notes = dto.Notes;
        shelter.UpdatedAtUtc = Now;
        uow.Repository<Shelter>().Update(shelter);

        await audit.RecordAsync("ShelterUpdated", nameof(Shelter), id.ToString(), $"capacity {shelter.Capacity}", ct: ct);
        await uow.SaveChangesAsync(ct);
        return ToDto(shelter);
    }

    public async Task<ShelterDto> SetStatusAsync(Guid id, ShelterStatus status, CancellationToken ct = default)
    {
        RequireManage();
        var shelter = await LoadShelterAsync(id, ct);
        if (status == ShelterStatus.Closed && shelter.Occupancy > 0)
            throw new ConflictException($"{shelter.Occupancy} people are still placed here. Release them before closing the shelter.");

        shelter.Status = status;
        shelter.UpdatedAtUtc = Now;
        uow.Repository<Shelter>().Update(shelter);
        await audit.RecordAsync("ShelterStatusChanged", nameof(Shelter), id.ToString(), status.ToString(), ct: ct);
        await uow.SaveChangesAsync(ct);
        return ToDto(shelter);
    }

    public async Task<IReadOnlyList<ShelterDto>> ListAsync(Guid? crisisEventId, CancellationToken ct = default)
    {
        RequireView();
        var cid = crisisEventId;
        Expression<Func<Shelter, bool>> where = s => cid == null || s.CrisisEventId == cid || s.CrisisEventId == null;
        var list = await uow.Repository<Shelter>().ListAsync(where, q => q.OrderBy(s => s.Name), ct: ct);
        return list.Select(s => ToDto(s)).ToList();
    }

    public async Task<ShelterDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        RequireView();
        return ToDto(await LoadShelterAsync(id, ct));
    }

    public async Task<IReadOnlyList<PublicShelterDto>> ListPublicAsync(Guid? crisisEventId, CancellationToken ct = default)
    {
        var cid = crisisEventId;
        Expression<Func<Shelter, bool>> where = s =>
            s.IsPublic && s.Status == ShelterStatus.Open && (cid == null || s.CrisisEventId == cid || s.CrisisEventId == null);
        var list = await uow.Repository<Shelter>().ListAsync(where, q => q.OrderBy(s => s.Name), ct: ct);
        return list.Select(s => new PublicShelterDto(
            s.Id, s.Name, s.Address, s.Latitude, s.Longitude, AvailabilityOf(s),
            s.HasMedicalSupport, s.WheelchairAccessible, s.ProvidesFood, s.ProvidesWater, s.Notes)).ToList();
    }

    // =====================================================================
    // Recommendation (read-only)
    // =====================================================================
    public async Task<ShelterRecommendationDto> RecommendAsync(Guid requestId, CancellationToken ct = default)
    {
        RequireView();
        var request = await LoadRequestAsync(requestId, ct);
        var people = request.PeopleAffected;

        if (await uow.Repository<ShelterAllocation>().AnyAsync(
                a => a.RequestId == requestId && a.Status == ShelterAllocationStatus.Active, ct))
            return new ShelterRecommendationDto(requestId, people, Array.Empty<ShelterMatchDto>(),
                "This household is already placed in a shelter.");

        var ranked = await RankAsync(request, people, ct);
        var candidates = ranked.Take(5).Select(m => new ShelterMatchDto(
            m.Shelter.Id, m.Shelter.Name, m.Shelter.AvailableSpaces, m.DistanceKm, m.Score, m.Rationale, m.Warnings)).ToList();

        var message = candidates.Count == 0
            ? "No open shelter has enough space (and the access this household needs). Consider opening another shelter or requesting extra capacity from a partner organisation."
            : "Best match first. A person must approve the placement.";
        return new ShelterRecommendationDto(requestId, people, candidates, message);
    }

    // =====================================================================
    // Placement (human decision)
    // =====================================================================
    public async Task<ShelterAllocationDto> AllocateAsync(AllocateShelterDto dto, CancellationToken ct = default)
    {
        RequireManage();
        var request = await LoadRequestAsync(dto.RequestId, ct);
        if (request.Status != RequestStatus.Triaged)
            throw new ConflictException("People can only be placed for requests that a human reviewer has triaged.");

        var allocations = uow.Repository<ShelterAllocation>();
        if (await allocations.AnyAsync(a => a.RequestId == request.Id && a.Status == ShelterAllocationStatus.Active, ct))
            throw new ConflictException("This request is already placed in a shelter. Release it first to move the household.");

        var people = dto.PeopleCount ?? request.PeopleAffected;
        if (people < 1 || people > request.PeopleAffected)
            throw new AppValidationException($"People count must be between 1 and {request.PeopleAffected}.");

        var shelterRepo = uow.Repository<Shelter>();
        var shelter = await shelterRepo.GetByIdAsync(dto.ShelterId, ct) ?? throw new NotFoundException("Shelter not found.");
        if (shelter.Status != ShelterStatus.Open) throw new ConflictException("This shelter is closed.");
        if (shelter.CrisisEventId is { } shelterCrisis && shelterCrisis != request.CrisisEventId)
            throw new ConflictException("This shelter belongs to a different crisis event.");
        if (shelter.Occupancy + people > shelter.Capacity)
            throw new ConflictException($"Shelter capacity exceeded: only {shelter.AvailableSpaces} spaces left.");

        // Suitability warnings do not block a human decision but are recorded.
        var needs = ShelterNeeds.From(request);
        var warnings = new List<string>();
        if (needs.Wheelchair && !shelter.WheelchairAccessible)
            warnings.Add("The household may need wheelchair access, but this shelter is not accessible.");
        if (needs.Medical && !shelter.HasMedicalSupport)
            warnings.Add("The household may need medical care, but this shelter has no medical support on site.");

        var ranked = await RankAsync(request, people, ct);
        var position = ranked.ToList().FindIndex(m => m.Shelter.Id == shelter.Id);

        var now = Now;
        shelter.Occupancy += people;
        shelter.UpdatedAtUtc = now;
        shelterRepo.Update(shelter);

        var allocation = new ShelterAllocation
        {
            CrisisEventId = request.CrisisEventId,
            ShelterId = shelter.Id,
            RequestId = request.Id,
            PeopleCount = people,
            Status = ShelterAllocationStatus.Active,
            AllocatedByUserId = current.UserId ?? Guid.Empty,
            AllocatedByEmail = current.Email,
            AllocatedAtUtc = now,
            Note = dto.Note,
            CreatedAtUtc = now
        };
        await allocations.AddAsync(allocation, ct);

        await AddHistoryAsync(request.Id, "ShelterAllocated", $"{people} people placed in {shelter.Name}", true, ct);
        await Events.PublishAsync(new ShelterPlacementMade(
            allocation.Id, request.Id, request.RequesterId, shelter.Name, shelter.Address, people), ct);
        await audit.RecordAsync("ShelterAllocated", nameof(AssistanceRequest), request.Id.ToString(),
            JsonSerializer.Serialize(new
            {
                allocationId = allocation.Id,
                shelterId = shelter.Id,
                people,
                warnings,
                recommendedFirst = ranked.Count > 0 ? ranked[0].Shelter.Id : (Guid?)null,
                humanChoiceRank = position >= 0 ? position + 1 : (int?)null,
                note = dto.Note
            }), ct: ct);

        await uow.SaveChangesAsync(ct);
        return ToDto(allocation, warnings);
    }

    public async Task<ShelterAllocationDto> ReleaseAsync(Guid allocationId, ReleaseShelterDto dto, CancellationToken ct = default)
    {
        RequireManage();
        var allocation = await uow.Repository<ShelterAllocation>().GetByIdAsync(allocationId, ct)
                         ?? throw new NotFoundException("Placement not found.");
        if (allocation.Status != ShelterAllocationStatus.Active)
            throw new ConflictException("This placement has already ended.");

        var shelter = await LoadShelterAsync(allocation.ShelterId, ct);
        var now = Now;
        shelter.Occupancy = Math.Max(0, shelter.Occupancy - allocation.PeopleCount);
        shelter.UpdatedAtUtc = now;
        uow.Repository<Shelter>().Update(shelter);

        allocation.Status = ShelterAllocationStatus.CheckedOut;
        allocation.ReleasedAtUtc = now;
        allocation.ReleaseReason = dto.Reason.Trim();
        allocation.UpdatedAtUtc = now;
        uow.Repository<ShelterAllocation>().Update(allocation);

        await AddHistoryAsync(allocation.RequestId, "ShelterReleased", dto.Reason.Trim(), false, ct);
        await audit.RecordAsync("ShelterReleased", nameof(ShelterAllocation), allocation.Id.ToString(), dto.Reason.Trim(), ct: ct);
        await uow.SaveChangesAsync(ct);
        return ToDto(allocation, Array.Empty<string>());
    }

    public async Task<IReadOnlyList<ShelterAllocationDto>> ListAllocationsAsync(Guid shelterId, bool activeOnly, CancellationToken ct = default)
    {
        RequireView();
        var onlyActive = activeOnly;
        var list = await uow.Repository<ShelterAllocation>().ListAsync(
            a => a.ShelterId == shelterId && (!onlyActive || a.Status == ShelterAllocationStatus.Active),
            q => q.OrderByDescending(a => a.AllocatedAtUtc), ct: ct);
        return list.Select(a => ToDto(a, Array.Empty<string>())).ToList();
    }

    // =====================================================================
    // Supply requests (shelter -> logistics)
    // =====================================================================
    public async Task<SupplyRequestDto> RequestSuppliesAsync(Guid shelterId, CreateSupplyRequestDto dto, CancellationToken ct = default)
    {
        RequireManage();
        var shelter = await LoadShelterAsync(shelterId, ct);
        var now = Now;
        var supply = new ShelterSupplyRequest
        {
            ShelterId = shelter.Id,
            Category = dto.Category,
            Quantity = dto.Quantity,
            Note = dto.Note,
            RequestedByUserId = current.UserId ?? Guid.Empty,
            RequestedByEmail = current.Email,
            CreatedAtUtc = now
        };
        await uow.Repository<ShelterSupplyRequest>().AddAsync(supply, ct);
        await audit.RecordAsync("ShelterSupplyRequested", nameof(Shelter), shelter.Id.ToString(),
            $"{dto.Quantity} x {dto.Category}", ct: ct);
        await uow.SaveChangesAsync(ct);
        return ToDto(supply);
    }

    public async Task<IReadOnlyList<SupplyRequestDto>> ListSupplyRequestsAsync(SupplyRequestStatus? status, CancellationToken ct = default)
    {
        RequireSupplies();
        var st = status;
        var list = await uow.Repository<ShelterSupplyRequest>().ListAsync(
            r => st == null || r.Status == st, q => q.OrderByDescending(r => r.CreatedAtUtc), ct: ct);
        return list.Select(r => ToDto(r)).ToList();
    }

    public async Task<SupplyRequestDto> ResolveSupplyRequestAsync(Guid id, ResolveSupplyRequestDto dto, CancellationToken ct = default)
    {
        RequireSupplies();
        var supply = await uow.Repository<ShelterSupplyRequest>().GetByIdAsync(id, ct)
                     ?? throw new NotFoundException("Supply request not found.");
        if (supply.Status != SupplyRequestStatus.Open)
            throw new ConflictException($"This supply request is already {supply.Status}.");

        supply.Status = SupplyRequestStatus.Fulfilled;
        supply.ResolvedAtUtc = Now;
        supply.ResolutionNote = dto.Note;
        supply.UpdatedAtUtc = Now;
        uow.Repository<ShelterSupplyRequest>().Update(supply);
        await audit.RecordAsync("ShelterSupplyFulfilled", nameof(ShelterSupplyRequest), id.ToString(), dto.Note, ct: ct);
        await uow.SaveChangesAsync(ct);
        return ToDto(supply);
    }

    // =====================================================================
    // Helpers
    // =====================================================================
    private async Task<IReadOnlyList<ShelterMatch>> RankAsync(AssistanceRequest request, int people, CancellationToken ct)
    {
        var crisisId = request.CrisisEventId;
        var open = await uow.Repository<Shelter>().ListAsync(
            s => s.Status == ShelterStatus.Open && (s.CrisisEventId == null || s.CrisisEventId == crisisId), ct: ct);
        return matcher.Rank(request, people, open);
    }

    private static string AvailabilityOf(Shelter s) =>
        s.AvailableSpaces == 0 ? "Full" : s.AvailableSpaces * 5 <= s.Capacity ? "Limited" : "Available";

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

    private async Task<Shelter> LoadShelterAsync(Guid id, CancellationToken ct) =>
        await uow.Repository<Shelter>().GetByIdAsync(id, ct) ?? throw new NotFoundException("Shelter not found.");

    private async Task<AssistanceRequest> LoadRequestAsync(Guid id, CancellationToken ct) =>
        await uow.Repository<AssistanceRequest>().GetByIdAsync(id, ct) ?? throw new NotFoundException("Request not found.");

    private void RequireManage()
    {
        if (!CanManage) throw new ForbiddenException("Only shelter administrators and emergency coordinators can manage shelters.");
    }

    private void RequireView()
    {
        if (!CanView) throw new ForbiddenException("You are not allowed to view shelter details.");
    }

    private void RequireSupplies()
    {
        if (!CanSupplies) throw new ForbiddenException("You are not allowed to handle shelter supply requests.");
    }

    public static ShelterDto ToDto(Shelter s) => new(
        s.Id, s.CrisisEventId, s.Name, s.Address, s.Latitude, s.Longitude, s.Capacity, s.Occupancy, s.AvailableSpaces,
        s.Capacity == 0 ? 0 : s.Occupancy * 100 / s.Capacity, s.AvailableSpaces == 0,
        s.HasMedicalSupport, s.WheelchairAccessible, s.ProvidesFood, s.ProvidesWater,
        s.Status.ToString(), s.IsPublic, s.Notes);

    private static ShelterAllocationDto ToDto(ShelterAllocation a, IReadOnlyList<string> warnings) => new(
        a.Id, a.ShelterId, a.RequestId, a.PeopleCount, a.Status.ToString(), a.AllocatedByUserId, a.AllocatedByEmail,
        a.AllocatedAtUtc, a.ReleasedAtUtc, a.Note, a.ReleaseReason, warnings);

    private static SupplyRequestDto ToDto(ShelterSupplyRequest r) => new(
        r.Id, r.ShelterId, r.Category.ToString(), r.Quantity, r.Status.ToString(), r.Note, r.RequestedByEmail,
        r.CreatedAtUtc, r.ResolvedAtUtc, r.ResolutionNote);
}

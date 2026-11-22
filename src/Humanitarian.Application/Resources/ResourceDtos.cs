using System.ComponentModel.DataAnnotations;
using Humanitarian.Domain.Enums;

namespace Humanitarian.Application.Resources;

// ---------- input ----------
public record CreateResourceDto(
    [Required, StringLength(150, MinimumLength = 2)] string Name,
    [EnumDataType(typeof(ResourceCategory))] ResourceCategory Category,
    [Required, StringLength(30)] string Unit,
    [Range(0, 1000000)] int QuantityTotal,
    [Range(0, 1000000)] int ReorderLevel,
    [StringLength(200)] string? LocationName,
    [Range(-90, 90)] double? Latitude,
    [Range(-180, 180)] double? Longitude,
    Guid? OrganisationId);

public record RestockDto(
    [Range(1, 1000000)] int Quantity,
    [StringLength(300)] string? Note);

public record AllocateDto(
    [Required] Guid RequestId,
    [Required] Guid ResourceItemId,
    [Range(1, 1000000)] int Quantity,
    [StringLength(500)] string? Note);

public record CancelAllocationDto(
    [Required, StringLength(300, MinimumLength = 3)] string Reason);

// ---------- output ----------
public record ResourceDto(
    Guid Id, string Name, string Category, string Unit, int QuantityTotal, int QuantityAvailable, int ReorderLevel,
    string? LocationName, double? Latitude, double? Longitude, bool IsActive, int UtilisationPercent,
    bool IsLowStock, Guid? OrganisationId);

public record AllocationDto(
    Guid Id, Guid CrisisEventId, Guid RequestId, Guid ResourceItemId, int Quantity, string Status, double Score,
    string Rationale, Guid ApprovedByUserId, string? ApprovedByEmail, DateTime ApprovedAtUtc,
    DateTime? DispatchedAtUtc, DateTime? DeliveredAtUtc, string? CancelReason);

public record MatchCandidateDto(
    Guid ResourceItemId, string Name, string Category, int QuantityAvailable, double? DistanceKm,
    double Score, bool CanFullyCover, string Rationale);

public record MatchResultDto(
    Guid RequestId, string ResourceCategory, int QuantityNeeded,
    IReadOnlyList<MatchCandidateDto> Candidates, string Message);

public record PlanTakeDto(Guid ResourceItemId, string ResourceName, int Quantity);

public record PlanLineDto(
    Guid RequestId, int PriorityScore, string Urgency, int PeopleAffected, int QuantityNeeded,
    int QuantityProposed, string Outcome, IReadOnlyList<PlanTakeDto> Takes);

public record ShortagePlanDto(
    Guid CrisisEventId, string ResourceCategory, int TotalNeeded, int TotalAvailable, int Shortfall,
    bool HasShortage, IReadOnlyList<PlanLineDto> Lines);

public record ApplyPlanResultDto(int AllocationsCreated, int RequestsNotServed, IReadOnlyList<AllocationDto> Allocations);

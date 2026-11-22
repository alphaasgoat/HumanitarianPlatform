using System.ComponentModel.DataAnnotations;
using Humanitarian.Domain.Enums;

namespace Humanitarian.Application.Shelters;

// ---------- input ----------
public record CreateShelterDto(
    [Required, StringLength(150, MinimumLength = 2)] string Name,
    [Required, StringLength(300)] string Address,
    [Range(-90, 90)] double? Latitude,
    [Range(-180, 180)] double? Longitude,
    [Range(1, 100000)] int Capacity,
    bool HasMedicalSupport,
    bool WheelchairAccessible,
    bool ProvidesFood,
    bool ProvidesWater,
    bool IsPublic,
    Guid? CrisisEventId,
    [StringLength(500)] string? Notes);

public record UpdateShelterDto(
    [Range(1, 100000)] int Capacity,
    bool HasMedicalSupport,
    bool WheelchairAccessible,
    bool ProvidesFood,
    bool ProvidesWater,
    bool IsPublic,
    [StringLength(500)] string? Notes);

public record AllocateShelterDto(
    [Required] Guid RequestId,
    [Required] Guid ShelterId,
    [Range(1, 10000)] int? PeopleCount,
    [StringLength(500)] string? Note);

public record ReleaseShelterDto([Required, StringLength(300, MinimumLength = 3)] string Reason);

public record CreateSupplyRequestDto(
    [EnumDataType(typeof(ResourceCategory))] ResourceCategory Category,
    [Range(1, 100000)] int Quantity,
    [StringLength(300)] string? Note);

public record ResolveSupplyRequestDto([StringLength(300)] string? Note);

// ---------- output ----------
public record ShelterDto(
    Guid Id, Guid? CrisisEventId, string Name, string Address, double? Latitude, double? Longitude,
    int Capacity, int Occupancy, int AvailableSpaces, int OccupancyPercent, bool IsFull,
    bool HasMedicalSupport, bool WheelchairAccessible, bool ProvidesFood, bool ProvidesWater,
    string Status, bool IsPublic, string? Notes);

/// <summary>What affected people may see: no exact occupancy, only a coarse availability level.</summary>
public record PublicShelterDto(
    Guid Id, string Name, string Address, double? Latitude, double? Longitude, string Availability,
    bool HasMedicalSupport, bool WheelchairAccessible, bool ProvidesFood, bool ProvidesWater, string? Notes);

public record ShelterMatchDto(
    Guid ShelterId, string Name, int AvailableSpaces, double? DistanceKm, double Score,
    string Rationale, IReadOnlyList<string> Warnings);

public record ShelterRecommendationDto(
    Guid RequestId, int PeopleCount, IReadOnlyList<ShelterMatchDto> Candidates, string Message);

public record ShelterAllocationDto(
    Guid Id, Guid ShelterId, Guid RequestId, int PeopleCount, string Status, Guid AllocatedByUserId,
    string? AllocatedByEmail, DateTime AllocatedAtUtc, DateTime? ReleasedAtUtc, string? Note,
    string? ReleaseReason, IReadOnlyList<string> Warnings);

public record SupplyRequestDto(
    Guid Id, Guid ShelterId, string Category, int Quantity, string Status, string? Note,
    string? RequestedByEmail, DateTime CreatedAtUtc, DateTime? ResolvedAtUtc, string? ResolutionNote);

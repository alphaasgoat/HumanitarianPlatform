using System.ComponentModel.DataAnnotations;
using Humanitarian.Domain.Enums;

namespace Humanitarian.Application.Volunteers;

// ---------- input ----------
public record RegisterVolunteerDto(
    [Required, StringLength(150, MinimumLength = 2)] string FullName,
    [Phone, StringLength(30)] string? Phone,
    [Required, MinLength(1)] List<VolunteerSkill> Skills,
    [Range(-90, 90)] double? Latitude,
    [Range(-180, 180)] double? Longitude);

public record AvailabilityDto([Required] DateTime StartUtc, [Required] DateTime EndUtc);

public record CreateTaskDto(
    [Required] Guid CrisisEventId,
    Guid? RequestId,
    [Required, StringLength(150, MinimumLength = 3)] string Title,
    [StringLength(1000)] string? Description,
    [EnumDataType(typeof(VolunteerSkill))] VolunteerSkill RequiredSkill,
    [Range(1, 500)] int VolunteersNeeded,
    [Range(-90, 90)] double? Latitude,
    [Range(-180, 180)] double? Longitude,
    [Required] DateTime StartUtc,
    [Required] DateTime EndUtc);

public record AssignDto([Required] Guid VolunteerId, [StringLength(300)] string? Note);

public record CompleteAssignmentDto([Range(0.25, 24)] double HoursWorked, [StringLength(300)] string? Note);

public record ReasonDto([StringLength(300)] string? Reason);

// ---------- output ----------
public record VolunteerDto(
    Guid Id, Guid UserId, string FullName, string? Phone, IReadOnlyList<string> Skills, double? Latitude,
    double? Longitude, bool IsVerified, bool IsActive, double TotalHours, int CompletedTasks);

public record AvailabilitySlotDto(Guid Id, DateTime StartUtc, DateTime EndUtc);

public record TaskDto(
    Guid Id, Guid CrisisEventId, Guid? RequestId, string Title, string? Description, string RequiredSkill,
    int VolunteersNeeded, int ActiveAssignments, double? Latitude, double? Longitude,
    DateTime StartUtc, DateTime EndUtc, string Status);

public record VolunteerMatchDto(
    Guid VolunteerId, string FullName, IReadOnlyList<string> Skills, double? DistanceKm, double Score, string Rationale);

public record AssignmentDto(
    Guid Id, Guid TaskId, Guid VolunteerId, string Status, double MatchScore, DateTime AssignedAtUtc,
    DateTime? RespondedAtUtc, DateTime? CompletedAtUtc, double? HoursWorked, string? Note,
    string? TaskTitle, DateTime? TaskStartUtc, DateTime? TaskEndUtc);

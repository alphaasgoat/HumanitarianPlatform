using System.Linq.Expressions;
using Humanitarian.Application.Common;
using Humanitarian.Domain.Entities;
using Humanitarian.Domain.Enums;

namespace Humanitarian.Application.Volunteers;

public interface IVolunteerService
{
    Task<VolunteerDto> RegisterMeAsync(RegisterVolunteerDto dto, CancellationToken ct = default);
    Task<VolunteerDto> GetMeAsync(CancellationToken ct = default);
    Task<VolunteerDto> UpdateMeAsync(RegisterVolunteerDto dto, CancellationToken ct = default);
    Task<AvailabilitySlotDto> AddAvailabilityAsync(AvailabilityDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<AvailabilitySlotDto>> ListMyAvailabilityAsync(CancellationToken ct = default);

    Task<IReadOnlyList<VolunteerDto>> ListAsync(VolunteerSkill? skill, bool? verified, CancellationToken ct = default);
    Task<VolunteerDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<VolunteerDto> VerifyAsync(Guid id, CancellationToken ct = default);
    Task<VolunteerDto> SetActiveAsync(Guid id, bool active, CancellationToken ct = default);
}

/// <summary>Volunteer profiles, availability scheduling and verification.</summary>
public class VolunteerService(IUnitOfWork uow, IAuditService audit, ICurrentUser current, TimeProvider time) : IVolunteerService
{
    private DateTime Now => time.GetUtcNow().UtcDateTime;
    private bool CanCoordinate => current.Role is Roles.VolunteerCoordinator or Roles.EmergencyCoordinator;
    private bool CanView => CanCoordinate || current.Role == Roles.CaseManager;

    // =====================================================================
    // The volunteer's own profile (account role: Citizen)
    // =====================================================================
    public async Task<VolunteerDto> RegisterMeAsync(RegisterVolunteerDto dto, CancellationToken ct = default)
    {
        var userId = RequireCitizen();
        var repo = uow.Repository<Volunteer>();
        if (await repo.AnyAsync(v => v.UserId == userId, ct))
            throw new ConflictException("You are already registered as a volunteer.");
        RequireLocationPair(dto.Latitude, dto.Longitude);

        var volunteer = new Volunteer
        {
            UserId = userId,
            FullName = dto.FullName.Trim(),
            Phone = dto.Phone,
            Skills = SkillSet.Format(dto.Skills),
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            CreatedAtUtc = Now
        };
        await repo.AddAsync(volunteer, ct);
        await audit.RecordAsync("VolunteerRegistered", nameof(Volunteer), volunteer.Id.ToString(), volunteer.Skills, ct: ct);
        await uow.SaveChangesAsync(ct);
        return (await ToDtosAsync(new[] { volunteer }, ct))[0];
    }

    public async Task<VolunteerDto> GetMeAsync(CancellationToken ct = default)
    {
        var volunteer = await LoadMineAsync(ct);
        return (await ToDtosAsync(new[] { volunteer }, ct))[0];
    }

    public async Task<VolunteerDto> UpdateMeAsync(RegisterVolunteerDto dto, CancellationToken ct = default)
    {
        var volunteer = await LoadMineAsync(ct);
        RequireLocationPair(dto.Latitude, dto.Longitude);

        var newSkills = SkillSet.Format(dto.Skills);
        var skillsChanged = newSkills != volunteer.Skills;

        volunteer.FullName = dto.FullName.Trim();
        volunteer.Phone = dto.Phone;
        volunteer.Skills = newSkills;
        volunteer.Latitude = dto.Latitude;
        volunteer.Longitude = dto.Longitude;
        volunteer.UpdatedAtUtc = Now;

        // New skill claims (for example "Medical") must be checked again by a coordinator.
        if (skillsChanged && volunteer.IsVerified)
        {
            volunteer.IsVerified = false;
            volunteer.VerifiedByUserId = null;
            volunteer.VerifiedAtUtc = null;
        }

        uow.Repository<Volunteer>().Update(volunteer);
        await audit.RecordAsync("VolunteerUpdated", nameof(Volunteer), volunteer.Id.ToString(),
            skillsChanged ? "Skills changed: verification reset." : null, ct: ct);
        await uow.SaveChangesAsync(ct);
        return (await ToDtosAsync(new[] { volunteer }, ct))[0];
    }

    public async Task<AvailabilitySlotDto> AddAvailabilityAsync(AvailabilityDto dto, CancellationToken ct = default)
    {
        var volunteer = await LoadMineAsync(ct);
        var start = TimeRules.AsUtc(dto.StartUtc);
        var end = TimeRules.AsUtc(dto.EndUtc);
        if (end <= start) throw new AppValidationException("The end time must be after the start time.");
        if (end - start > TimeSpan.FromDays(14)) throw new AppValidationException("A single availability window can be at most 14 days.");
        if (end <= Now) throw new AppValidationException("The availability window must end in the future.");

        var slots = uow.Repository<VolunteerAvailability>();
        var id = volunteer.Id;
        if (await slots.AnyAsync(s => s.VolunteerId == id && s.StartUtc < end && start < s.EndUtc, ct))
            throw new ConflictException("This window overlaps one you already added.");

        var slot = new VolunteerAvailability { VolunteerId = volunteer.Id, StartUtc = start, EndUtc = end, CreatedAtUtc = Now };
        await slots.AddAsync(slot, ct);
        await uow.SaveChangesAsync(ct);
        return new AvailabilitySlotDto(slot.Id, slot.StartUtc, slot.EndUtc);
    }

    public async Task<IReadOnlyList<AvailabilitySlotDto>> ListMyAvailabilityAsync(CancellationToken ct = default)
    {
        var volunteer = await LoadMineAsync(ct);
        var id = volunteer.Id;
        var slots = await uow.Repository<VolunteerAvailability>().ListAsync(
            s => s.VolunteerId == id, q => q.OrderBy(s => s.StartUtc), ct: ct);
        return slots.Select(s => new AvailabilitySlotDto(s.Id, s.StartUtc, s.EndUtc)).ToList();
    }

    // =====================================================================
    // Coordinators: list, verify, deactivate
    // =====================================================================
    public async Task<IReadOnlyList<VolunteerDto>> ListAsync(VolunteerSkill? skill, bool? verified, CancellationToken ct = default)
    {
        RequireView();
        var ver = verified;
        Expression<Func<Volunteer, bool>> where = v => ver == null || v.IsVerified == ver;
        var list = await uow.Repository<Volunteer>().ListAsync(where, q => q.OrderBy(v => v.FullName), ct: ct);
        IEnumerable<Volunteer> filtered = list;
        if (skill is { } s) filtered = filtered.Where(v => SkillSet.Parse(v.Skills).Contains(s));
        return await ToDtosAsync(filtered.ToList(), ct);
    }

    public async Task<VolunteerDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        RequireView();
        var volunteer = await uow.Repository<Volunteer>().GetByIdAsync(id, ct) ?? throw new NotFoundException("Volunteer not found.");
        return (await ToDtosAsync(new[] { volunteer }, ct))[0];
    }

    public async Task<VolunteerDto> VerifyAsync(Guid id, CancellationToken ct = default)
    {
        RequireCoordinate();
        var repo = uow.Repository<Volunteer>();
        var volunteer = await repo.GetByIdAsync(id, ct) ?? throw new NotFoundException("Volunteer not found.");
        if (volunteer.IsVerified) throw new ConflictException("This volunteer is already verified.");

        volunteer.IsVerified = true;
        volunteer.VerifiedByUserId = current.UserId;
        volunteer.VerifiedAtUtc = Now;
        volunteer.UpdatedAtUtc = Now;
        repo.Update(volunteer);
        await audit.RecordAsync("VolunteerVerified", nameof(Volunteer), id.ToString(), volunteer.Skills, ct: ct);
        await uow.SaveChangesAsync(ct);
        return (await ToDtosAsync(new[] { volunteer }, ct))[0];
    }

    public async Task<VolunteerDto> SetActiveAsync(Guid id, bool active, CancellationToken ct = default)
    {
        RequireCoordinate();
        var repo = uow.Repository<Volunteer>();
        var volunteer = await repo.GetByIdAsync(id, ct) ?? throw new NotFoundException("Volunteer not found.");
        volunteer.IsActive = active;
        volunteer.UpdatedAtUtc = Now;
        repo.Update(volunteer);
        await audit.RecordAsync(active ? "VolunteerActivated" : "VolunteerDeactivated", nameof(Volunteer), id.ToString(), ct: ct);
        await uow.SaveChangesAsync(ct);
        return (await ToDtosAsync(new[] { volunteer }, ct))[0];
    }

    // =====================================================================
    // Helpers
    // =====================================================================
    private Guid RequireCitizen()
    {
        if (current.Role != Roles.Citizen || current.UserId is null)
            throw new ForbiddenException("Only signed-in citizens can register as volunteers.");
        return current.UserId.Value;
    }

    private async Task<Volunteer> LoadMineAsync(CancellationToken ct)
    {
        var userId = RequireCitizen();
        return await uow.Repository<Volunteer>().FirstOrDefaultAsync(v => v.UserId == userId, ct)
               ?? throw new NotFoundException("You have not registered as a volunteer yet.");
    }

    private static void RequireLocationPair(double? lat, double? lon)
    {
        if (lat.HasValue != lon.HasValue) throw new AppValidationException("Latitude and longitude must be provided together.");
    }

    private void RequireCoordinate()
    {
        if (!CanCoordinate) throw new ForbiddenException("Only volunteer coordinators and emergency coordinators can do this.");
    }

    private void RequireView()
    {
        if (!CanView) throw new ForbiddenException("You are not allowed to view volunteer profiles.");
    }

    private async Task<IReadOnlyList<VolunteerDto>> ToDtosAsync(IReadOnlyList<Volunteer> volunteers, CancellationToken ct)
    {
        if (volunteers.Count == 0) return Array.Empty<VolunteerDto>();
        var ids = volunteers.Select(v => v.Id).ToList();
        var completed = await uow.Repository<TaskAssignment>().ListAsync(
            a => ids.Contains(a.VolunteerId) && a.Status == AssignmentStatus.Completed, ct: ct);

        return volunteers.Select(v =>
        {
            var mine = completed.Where(a => a.VolunteerId == v.Id).ToList();
            return new VolunteerDto(v.Id, v.UserId, v.FullName, v.Phone, SkillSet.Names(v.Skills), v.Latitude, v.Longitude,
                v.IsVerified, v.IsActive, Math.Round(mine.Sum(a => a.HoursWorked ?? 0), 2), mine.Count);
        }).ToList();
    }
}

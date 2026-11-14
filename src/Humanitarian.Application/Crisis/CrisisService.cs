using System.ComponentModel.DataAnnotations;
using Humanitarian.Application.Common;
using Humanitarian.Domain.Entities;
using Humanitarian.Domain.Enums;

namespace Humanitarian.Application.Crisis;

public record CreateCrisisRequest(
    [Required, StringLength(200, MinimumLength = 3)] string Name,
    [EnumDataType(typeof(CrisisType))] CrisisType Type,
    [Required, StringLength(100)] string Country,
    [StringLength(2000)] string? Description);

public record ChangeCrisisStatusRequest([EnumDataType(typeof(CrisisStatus))] CrisisStatus Status);

public record AddAreaRequest(
    [Required, StringLength(150)] string Name,
    [Range(-90, 90)] double Latitude,
    [Range(-180, 180)] double Longitude,
    [Range(0.1, 5000)] double RadiusKm);

public record AreaDto(Guid Id, string Name, double Latitude, double Longitude, double RadiusKm);

public record CrisisDto(Guid Id, string Name, string Type, string Status, string Country, string? Description,
    DateTime StartedAtUtc, DateTime? ClosedAtUtc, IReadOnlyList<AreaDto> Areas);

public interface ICrisisService
{
    Task<CrisisDto> CreateAsync(CreateCrisisRequest r, CancellationToken ct = default);
    Task<IReadOnlyList<CrisisDto>> ListAsync(CrisisStatus? status, CancellationToken ct = default);
    Task<CrisisDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<CrisisDto> ChangeStatusAsync(Guid id, CrisisStatus status, CancellationToken ct = default);
    Task<CrisisDto> AddAreaAsync(Guid crisisId, AddAreaRequest r, CancellationToken ct = default);
}

public class CrisisService(IUnitOfWork uow, IAuditService audit, ICurrentUser current, TimeProvider time) : ICrisisService
{
    static CrisisDto ToDto(CrisisEvent c) => new(c.Id, c.Name, c.Type.ToString(), c.Status.ToString(), c.Country,
        c.Description, c.StartedAtUtc, c.ClosedAtUtc,
        c.Areas.Select(a => new AreaDto(a.Id, a.Name, a.Latitude, a.Longitude, a.RadiusKm)).ToList());

    public async Task<CrisisDto> CreateAsync(CreateCrisisRequest r, CancellationToken ct = default)
    {
        var userId = current.UserId ?? throw new ForbiddenException("Authenticated user required.");
        var c = new CrisisEvent
        {
            Name = r.Name.Trim(), Type = r.Type, Country = r.Country.Trim(), Description = r.Description,
            StartedAtUtc = time.GetUtcNow().UtcDateTime, CreatedByUserId = userId
        };
        await uow.Repository<CrisisEvent>().AddAsync(c, ct);
        await audit.RecordAsync("CrisisCreated", nameof(CrisisEvent), c.Id.ToString(), c.Name, ct: ct);
        await uow.SaveChangesAsync(ct);
        return ToDto(c);
    }

    public async Task<IReadOnlyList<CrisisDto>> ListAsync(CrisisStatus? status, CancellationToken ct = default)
    {
        var list = await uow.Repository<CrisisEvent>().ListAsync(
            status is null ? null : c => c.Status == status,
            q => q.OrderByDescending(c => c.StartedAtUtc), ct: ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<CrisisDto> GetAsync(Guid id, CancellationToken ct = default) =>
        ToDto(await Load(id, ct));

    public async Task<CrisisDto> ChangeStatusAsync(Guid id, CrisisStatus status, CancellationToken ct = default)
    {
        var c = await Load(id, ct);
        var old = c.Status;
        var now = time.GetUtcNow().UtcDateTime;
        c.Status = status;
        c.ClosedAtUtc = status == CrisisStatus.Closed ? now : null;
        c.UpdatedAtUtc = now;
        uow.Repository<CrisisEvent>().Update(c);
        await audit.RecordAsync("CrisisStatusChanged", nameof(CrisisEvent), id.ToString(), $"{old} -> {status}", ct: ct);
        await uow.SaveChangesAsync(ct);
        return ToDto(c);
    }

    public async Task<CrisisDto> AddAreaAsync(Guid crisisId, AddAreaRequest r, CancellationToken ct = default)
    {
        var c = await Load(crisisId, ct);
        if (c.Status == CrisisStatus.Closed) throw new ConflictException("Cannot add areas to a closed crisis.");
        var area = new OperationalArea
        {
            CrisisEventId = crisisId, Name = r.Name.Trim(),
            Latitude = r.Latitude, Longitude = r.Longitude, RadiusKm = r.RadiusKm
        };
        await uow.Repository<OperationalArea>().AddAsync(area, ct);
        c.Areas.Add(area);
        await audit.RecordAsync("AreaAdded", nameof(OperationalArea), area.Id.ToString(), area.Name, ct: ct);
        await uow.SaveChangesAsync(ct);
        return ToDto(c);
    }

    private async Task<CrisisEvent> Load(Guid id, CancellationToken ct) =>
        await uow.Repository<CrisisEvent>().GetByIdAsync(id, ct)
        ?? throw new NotFoundException("Crisis event not found.");
}


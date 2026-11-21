using System.Linq.Expressions;
using System.Text.Json;
using Humanitarian.Application.Common;
using Humanitarian.Domain.Entities;
using Humanitarian.Domain.Enums;

namespace Humanitarian.Application.Resources;

public interface IResourceService
{
    Task<ResourceDto> CreateAsync(CreateResourceDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<ResourceDto>> ListAsync(ResourceCategory? category, bool lowStockOnly, CancellationToken ct = default);
    Task<ResourceDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<ResourceDto> RestockAsync(Guid id, RestockDto dto, CancellationToken ct = default);
    Task<ResourceDto> SetActiveAsync(Guid id, bool active, CancellationToken ct = default);
}

/// <summary>Inventory management: stock levels, replenishment and low-stock detection.</summary>
public class ResourceService(IUnitOfWork uow, IAuditService audit, ICurrentUser current, TimeProvider time) : IResourceService
{
    private bool CanManage => current.Role is Roles.LogisticsManager or Roles.EmergencyCoordinator;
    private bool CanView => CanManage || current.Role == Roles.CaseManager;

    public static ResourceDto ToDto(ResourceItem r) => new(
        r.Id, r.Name, r.Category.ToString(), r.Unit, r.QuantityTotal, r.QuantityAvailable, r.ReorderLevel,
        r.LocationName, r.Latitude, r.Longitude, r.IsActive,
        r.QuantityTotal == 0 ? 0 : (r.QuantityTotal - r.QuantityAvailable) * 100 / r.QuantityTotal,
        r.ReorderLevel > 0 && r.QuantityAvailable <= r.ReorderLevel,
        r.OrganisationId);

    public async Task<ResourceDto> CreateAsync(CreateResourceDto dto, CancellationToken ct = default)
    {
        RequireManage();
        if (dto.Latitude.HasValue != dto.Longitude.HasValue)
            throw new AppValidationException("Latitude and longitude must be provided together.");
        if (dto.OrganisationId is { } orgId && !await uow.Repository<Organisation>().AnyAsync(o => o.Id == orgId, ct))
            throw new AppValidationException("Organisation does not exist.");

        var item = new ResourceItem
        {
            OrganisationId = dto.OrganisationId,
            Name = dto.Name.Trim(),
            Category = dto.Category,
            Unit = dto.Unit.Trim(),
            QuantityTotal = dto.QuantityTotal,
            QuantityAvailable = dto.QuantityTotal,
            ReorderLevel = dto.ReorderLevel,
            LocationName = dto.LocationName,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            CreatedAtUtc = time.GetUtcNow().UtcDateTime
        };
        await uow.Repository<ResourceItem>().AddAsync(item, ct);
        await audit.RecordAsync("ResourceCreated", nameof(ResourceItem), item.Id.ToString(),
            $"{item.Name}; {item.Category}; {item.QuantityTotal} {item.Unit}", ct: ct);
        await uow.SaveChangesAsync(ct);
        return ToDto(item);
    }

    public async Task<IReadOnlyList<ResourceDto>> ListAsync(ResourceCategory? category, bool lowStockOnly, CancellationToken ct = default)
    {
        RequireView();
        var cat = category;
        Expression<Func<ResourceItem, bool>> where = r => cat == null || r.Category == cat;
        var items = await uow.Repository<ResourceItem>().ListAsync(where, q => q.OrderBy(r => r.Name), ct: ct);
        var dtos = items.Select(r => ToDto(r));
        if (lowStockOnly) dtos = dtos.Where(d => d.IsLowStock);
        return dtos.ToList();
    }

    public async Task<ResourceDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        RequireView();
        return ToDto(await LoadAsync(id, ct));
    }

    public async Task<ResourceDto> RestockAsync(Guid id, RestockDto dto, CancellationToken ct = default)
    {
        RequireManage();
        var item = await LoadAsync(id, ct);
        item.QuantityTotal += dto.Quantity;
        item.QuantityAvailable += dto.Quantity;
        item.UpdatedAtUtc = time.GetUtcNow().UtcDateTime;
        uow.Repository<ResourceItem>().Update(item);
        await audit.RecordAsync("ResourceRestocked", nameof(ResourceItem), id.ToString(),
            JsonSerializer.Serialize(new { added = dto.Quantity, total = item.QuantityTotal, note = dto.Note }), ct: ct);
        await uow.SaveChangesAsync(ct);
        return ToDto(item);
    }

    public async Task<ResourceDto> SetActiveAsync(Guid id, bool active, CancellationToken ct = default)
    {
        RequireManage();
        var item = await LoadAsync(id, ct);
        item.IsActive = active;
        item.UpdatedAtUtc = time.GetUtcNow().UtcDateTime;
        uow.Repository<ResourceItem>().Update(item);
        await audit.RecordAsync(active ? "ResourceActivated" : "ResourceDeactivated", nameof(ResourceItem), id.ToString(), ct: ct);
        await uow.SaveChangesAsync(ct);
        return ToDto(item);
    }

    private void RequireManage()
    {
        if (!CanManage) throw new ForbiddenException("Only logistics managers and emergency coordinators can manage inventory.");
    }

    private void RequireView()
    {
        if (!CanView) throw new ForbiddenException("You are not allowed to view inventory.");
    }

    private async Task<ResourceItem> LoadAsync(Guid id, CancellationToken ct) =>
        await uow.Repository<ResourceItem>().GetByIdAsync(id, ct) ?? throw new NotFoundException("Resource not found.");
}

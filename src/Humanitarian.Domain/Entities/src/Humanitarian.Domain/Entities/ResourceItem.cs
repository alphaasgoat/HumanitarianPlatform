using Humanitarian.Domain.Common;
using Humanitarian.Domain.Enums;

namespace Humanitarian.Domain.Entities;

/// <summary>A stock of aid (e.g. 500 water bottles at a depot).</summary>
public class ResourceItem : BaseEntity
{
    public Guid? OrganisationId { get; set; }
    public string Name { get; set; } = "";
    public ResourceCategory Category { get; set; }
    public string Unit { get; set; } = "units";

    /// <summary>Everything physically held, including stock already reserved for requests.</summary>
    public int QuantityTotal { get; set; }

    /// <summary>Stock not yet reserved. Reserving lowers it; cancelling gives it back.</summary>
    public int QuantityAvailable { get; set; }

    /// <summary>When available stock falls to this level the item is flagged for replenishment.</summary>
    public int ReorderLevel { get; set; }

    public string? LocationName { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Optimistic-concurrency token: two people cannot reserve the same last units at once.</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

using Humanitarian.Domain.Common;
using Humanitarian.Domain.Enums;

namespace Humanitarian.Domain.Entities;

/// <summary>A shelter asking logistics for more supplies when stock on site is not enough.</summary>
public class ShelterSupplyRequest : BaseEntity
{
    public Guid ShelterId { get; set; }
    public ResourceCategory Category { get; set; }
    public int Quantity { get; set; }
    public string? Note { get; set; }
    public SupplyRequestStatus Status { get; set; } = SupplyRequestStatus.Open;
    public Guid RequestedByUserId { get; set; }
    public string? RequestedByEmail { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public string? ResolutionNote { get; set; }
}

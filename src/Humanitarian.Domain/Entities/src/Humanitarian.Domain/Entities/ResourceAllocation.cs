using Humanitarian.Domain.Common;
using Humanitarian.Domain.Enums;

namespace Humanitarian.Domain.Entities;

/// <summary>A human-approved reservation of resource stock for one assistance request.</summary>
public class ResourceAllocation : BaseEntity
{
    public Guid CrisisEventId { get; set; }
    public Guid RequestId { get; set; }
    public Guid ResourceItemId { get; set; }
    public int Quantity { get; set; }
    public AllocationStatus Status { get; set; } = AllocationStatus.Approved;

    /// <summary>Matcher score of the chosen resource at decision time (0 when allocated by a shortage plan).</summary>
    public double Score { get; set; }
    public string Rationale { get; set; } = "";

    public Guid ApprovedByUserId { get; set; }
    public string? ApprovedByEmail { get; set; }
    public DateTime ApprovedAtUtc { get; set; }
    public DateTime? DispatchedAtUtc { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }
    public string? CancelReason { get; set; }
}

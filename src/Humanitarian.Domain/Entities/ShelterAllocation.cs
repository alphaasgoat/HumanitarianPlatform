using Humanitarian.Domain.Common;
using Humanitarian.Domain.Enums;

namespace Humanitarian.Domain.Entities;

/// <summary>A household placed in a shelter (human decision).</summary>
public class ShelterAllocation : BaseEntity
{
    public Guid CrisisEventId { get; set; }
    public Guid ShelterId { get; set; }
    public Guid RequestId { get; set; }
    public int PeopleCount { get; set; }
    public ShelterAllocationStatus Status { get; set; } = ShelterAllocationStatus.Active;

    public Guid AllocatedByUserId { get; set; }
    public string? AllocatedByEmail { get; set; }
    public DateTime AllocatedAtUtc { get; set; }
    public DateTime? ReleasedAtUtc { get; set; }
    public string? Note { get; set; }
    public string? ReleaseReason { get; set; }
}

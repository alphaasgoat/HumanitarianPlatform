using Humanitarian.Domain.Common;
using Humanitarian.Domain.Enums;

namespace Humanitarian.Domain.Entities;

/// <summary>Timeline entry for a request (status changes and decisions).</summary>
public class RequestHistory : BaseEntity
{
    public Guid RequestId { get; set; }
    public string Action { get; set; } = "";
    public RequestStatus? FromStatus { get; set; }
    public RequestStatus? ToStatus { get; set; }
    public Guid? ActorUserId { get; set; }
    public string? ActorEmail { get; set; }
    public string? Note { get; set; }

    /// <summary>Public entries are visible to the affected person (without notes or actor).</summary>
    public bool IsPublic { get; set; }
}

using Humanitarian.Domain.Common;

namespace Humanitarian.Domain.Entities;

/// <summary>Append-only record of who did what. Never updated or deleted by the application.</summary>
public class AuditEvent : BaseEntity
{
    public Guid? ActorUserId { get; set; }
    public string? ActorEmail { get; set; }
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string? EntityId { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
}

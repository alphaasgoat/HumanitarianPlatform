using Humanitarian.Domain.Common;
using Humanitarian.Domain.Enums;

namespace Humanitarian.Domain.Entities;

public class CrisisEvent : BaseEntity
{
    public string Name { get; set; } = "";
    public CrisisType Type { get; set; }
    public CrisisStatus Status { get; set; } = CrisisStatus.Active;
    public string Country { get; set; } = "";
    public string? Description { get; set; }
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAtUtc { get; set; }
    public Guid CreatedByUserId { get; set; }
    public ICollection<OperationalArea> Areas { get; set; } = new List<OperationalArea>();
}

public class OperationalArea : BaseEntity
{
    public Guid CrisisEventId { get; set; }
    public CrisisEvent? CrisisEvent { get; set; }
    public string Name { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double RadiusKm { get; set; }
}

using Humanitarian.Domain.Common;

namespace Humanitarian.Domain.Entities;

/// <summary>A window of time (UTC) in which the volunteer can work.</summary>
public class VolunteerAvailability : BaseEntity
{
    public Guid VolunteerId { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
}

using Humanitarian.Domain.Common;
using Humanitarian.Domain.Enums;

namespace Humanitarian.Domain.Entities;

public class VolunteerTask : BaseEntity
{
    public Guid CrisisEventId { get; set; }
    public Guid? RequestId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public VolunteerSkill RequiredSkill { get; set; } = VolunteerSkill.General;
    public int VolunteersNeeded { get; set; } = 1;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public VolunteerTaskStatus Status { get; set; } = VolunteerTaskStatus.Open;
    public Guid CreatedByUserId { get; set; }
}

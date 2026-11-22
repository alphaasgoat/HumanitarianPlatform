using Humanitarian.Domain.Common;
using Humanitarian.Domain.Enums;

namespace Humanitarian.Domain.Entities;

public class TaskAssignment : BaseEntity
{
    public Guid TaskId { get; set; }
    public Guid VolunteerId { get; set; }
    public AssignmentStatus Status { get; set; } = AssignmentStatus.Assigned;
    public double MatchScore { get; set; }
    public Guid AssignedByUserId { get; set; }
    public DateTime AssignedAtUtc { get; set; }
    public DateTime? RespondedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public double? HoursWorked { get; set; }
    public string? Note { get; set; }
}

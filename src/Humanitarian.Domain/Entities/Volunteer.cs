using Humanitarian.Domain.Common;

namespace Humanitarian.Domain.Entities;

/// <summary>
/// A volunteer is a profile attached to an ordinary user account (role Citizen), so volunteers need no extra
/// role and can only ever see their own assignments.
/// </summary>
public class Volunteer : BaseEntity
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = "";
    public string? Phone { get; set; }

    /// <summary>Comma-separated skill names, e.g. "FirstAid,Driving".</summary>
    public string Skills { get; set; } = "";

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    /// <summary>Only verified volunteers can be assigned to tasks.</summary>
    public bool IsVerified { get; set; }
    public Guid? VerifiedByUserId { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }

    public bool IsActive { get; set; } = true;
}

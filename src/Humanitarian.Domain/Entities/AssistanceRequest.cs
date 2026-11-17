using Humanitarian.Domain.Common;
using Humanitarian.Domain.Enums;

namespace Humanitarian.Domain.Entities;

public class AssistanceRequest : BaseEntity
{
    public Guid CrisisEventId { get; set; }
    public Guid RequesterId { get; set; }

    /// <summary>Client-supplied key so a retried submission is not processed twice.</summary>
    public string? IdempotencyKey { get; set; }

    public RequestCategory Category { get; set; }
    public string Description { get; set; } = "";
    public int PeopleAffected { get; set; } = 1;

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? AddressText { get; set; }

    /// <summary>Urgency the affected person selected. Never changed after submission.</summary>
    public UrgencyLevel DeclaredUrgency { get; set; }

    /// <summary>Current working urgency. Only a human decision can change it.</summary>
    public UrgencyLevel Urgency { get; set; }

    /// <summary>0-100 score used to order the coordinator queue.</summary>
    public int PriorityScore { get; set; }

    public RequestStatus Status { get; set; } = RequestStatus.Submitted;
    public Guid? MergedIntoRequestId { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
}

using Humanitarian.Domain.Common;
using Humanitarian.Domain.Enums;

namespace Humanitarian.Domain.Entities;

/// <summary>
/// One AI suggestion plus the human decision taken on it. The Suggested* fields are never
/// overwritten, so the original recommendation stays available for post-incident review.
/// </summary>
public class AiRecommendation : BaseEntity
{
    public Guid RequestId { get; set; }

    public RequestCategory SuggestedCategory { get; set; }
    public UrgencyLevel SuggestedUrgency { get; set; }
    public int PriorityScore { get; set; }
    public double Confidence { get; set; }
    public string Rationale { get; set; } = "";
    public string ModelName { get; set; } = "";
    public bool UsedFallback { get; set; }

    public RecommendationStatus Status { get; set; } = RecommendationStatus.Pending;

    // Human decision
    public ReviewAction? HumanAction { get; set; }
    public RequestCategory? FinalCategory { get; set; }
    public UrgencyLevel? FinalUrgency { get; set; }
    public Guid? DecidedByUserId { get; set; }
    public string? DecidedByEmail { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionNote { get; set; }
}

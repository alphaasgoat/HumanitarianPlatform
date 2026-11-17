using Humanitarian.Domain.Common;
using Humanitarian.Domain.Enums;

namespace Humanitarian.Domain.Entities;

/// <summary>A suspected duplicate. Requests are never deleted: a human confirms (merge) or dismisses.</summary>
public class DuplicateLink : BaseEntity
{
    public Guid RequestId { get; set; }
    public Guid DuplicateOfRequestId { get; set; }
    public double SimilarityScore { get; set; }
    public string Reason { get; set; } = "";
    public DuplicateStatus Status { get; set; } = DuplicateStatus.Suspected;
}

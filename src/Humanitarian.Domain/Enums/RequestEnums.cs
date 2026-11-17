namespace Humanitarian.Domain.Enums;

public enum RequestCategory { Food = 1, Medicine, Water, Shelter, Transportation, Accessibility, Rescue, Other }

public enum UrgencyLevel { Low = 1, Medium = 2, High = 3, Critical = 4 }

public enum RequestStatus { Submitted, UnderReview, Triaged, Escalated, Merged, Closed }

/// <summary>What a human reviewer does with an AI recommendation.</summary>
public enum ReviewAction { Approve, Modify, Reject, Defer }

public enum RecommendationStatus { Pending, Approved, Modified, Rejected, Deferred, Superseded }

public enum DuplicateStatus { Suspected, ConfirmedMerged, Dismissed }

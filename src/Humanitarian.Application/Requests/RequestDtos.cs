using System.ComponentModel.DataAnnotations;
using Humanitarian.Domain.Enums;

namespace Humanitarian.Application.Requests;

// ---------- input ----------
public record SubmitRequestDto(
    [Required] Guid CrisisEventId,
    [EnumDataType(typeof(RequestCategory))] RequestCategory Category,
    [Required, StringLength(2000, MinimumLength = 10)] string Description,
    [Range(1, 10000)] int PeopleAffected,
    [EnumDataType(typeof(UrgencyLevel))] UrgencyLevel DeclaredUrgency,
    [Range(-90, 90)] double? Latitude,
    [Range(-180, 180)] double? Longitude,
    [StringLength(300)] string? AddressText,
    [StringLength(100)] string? IdempotencyKey);

public record ReviewRequestDto(
    [EnumDataType(typeof(ReviewAction))] ReviewAction Decision,
    [EnumDataType(typeof(RequestCategory))] RequestCategory? FinalCategory,
    [EnumDataType(typeof(UrgencyLevel))] UrgencyLevel? FinalUrgency,
    [StringLength(500)] string? Note);

public record MergeRequestDto(
    [Required] Guid DuplicateOfRequestId,
    [StringLength(500)] string? Note);

public record NoteRequestDto([StringLength(500)] string? Note);

public record RequestFilter(Guid? CrisisEventId, RequestStatus? Status, UrgencyLevel? Urgency, RequestCategory? Category);

// ---------- output ----------
public record RequestDto(
    Guid Id, Guid CrisisEventId, Guid RequesterId, string Category, string Description, int PeopleAffected,
    string DeclaredUrgency, string Urgency, string Status, double? Latitude, double? Longitude, string? AddressText,
    int PriorityScore, Guid? MergedIntoRequestId, DateTime CreatedAtUtc, DateTime? ClosedAtUtc);

public record RecommendationDto(
    Guid Id, string SuggestedCategory, string SuggestedUrgency, int PriorityScore, double Confidence,
    string Rationale, string ModelName, bool UsedFallback, string Status,
    string? HumanAction, string? FinalCategory, string? FinalUrgency,
    Guid? DecidedByUserId, string? DecidedByEmail, DateTime? DecidedAtUtc, string? DecisionNote, DateTime CreatedAtUtc);

public record DuplicateDto(Guid Id, Guid DuplicateOfRequestId, double SimilarityScore, string Reason, string Status);

public record TimelineEntryDto(
    DateTime TimestampUtc, string Action, string? FromStatus, string? ToStatus, string? ActorEmail, string? Note);

public record RequestDetailDto(
    RequestDto Request, RecommendationDto? LatestRecommendation,
    IReadOnlyList<DuplicateDto> PossibleDuplicates, IReadOnlyList<TimelineEntryDto> Timeline);

public record SubmitResultDto(RequestDetailDto Detail, bool WasExisting);

public record RequestPage(IReadOnlyList<RequestDto> Items, int Total, int Page, int PageSize);

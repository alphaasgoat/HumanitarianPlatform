using System.Linq.Expressions;
using System.Text.Json;
using Humanitarian.Application.Common;
using Humanitarian.Application.Mapping;
using Humanitarian.Application.Requests.Ai;
using Humanitarian.Application.Requests.Duplicates;
using Humanitarian.Domain.Entities;
using Humanitarian.Domain.Enums;

namespace Humanitarian.Application.Requests;

public interface IAssistanceRequestService
{
    Task<SubmitResultDto> SubmitAsync(SubmitRequestDto dto, CancellationToken ct = default);
    Task<RequestPage> GetMineAsync(int page, int pageSize, CancellationToken ct = default);
    Task<RequestPage> ListAsync(RequestFilter filter, int page, int pageSize, CancellationToken ct = default);
    Task<RequestDetailDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<RequestDetailDto> AnalyzeAsync(Guid id, CancellationToken ct = default);
    Task<RequestDetailDto> ReviewAsync(Guid id, ReviewRequestDto dto, CancellationToken ct = default);
    Task<RequestDetailDto> EscalateAsync(Guid id, string? note, CancellationToken ct = default);
    Task<RequestDetailDto> MergeAsync(Guid id, MergeRequestDto dto, CancellationToken ct = default);
    Task<RequestDetailDto> DismissDuplicateAsync(Guid id, Guid linkId, CancellationToken ct = default);
    Task<RequestDetailDto> CloseAsync(Guid id, string? note, CancellationToken ct = default);
}

/// <summary>
/// Core humanitarian workflow: submit -> AI recommendation -> human review (approve / modify /
/// reject / defer) -> triage. AI only ever SUGGESTS; every change to a request is made by a
/// person and written to the audit trail together with the original recommendation.
/// </summary>
public class AssistanceRequestService(
    IUnitOfWork uow, IAuditService audit, ICurrentUser current, TimeProvider time,
    IRequestAnalyzer analyzer, IDuplicateDetector detector,
    IMappingService? mapping = null, IDomainEventPublisher? events = null) : IAssistanceRequestService
{
    // Optional collaborators: without them the service behaves exactly as before (no geocoding, no events).
    private IDomainEventPublisher Events => events ?? NullDomainEventPublisher.Instance;

    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromHours(72);
    private DateTime Now => time.GetUtcNow().UtcDateTime;

    // =====================================================================
    // Submit (affected person)
    // =====================================================================
    public async Task<SubmitResultDto> SubmitAsync(SubmitRequestDto dto, CancellationToken ct = default)
    {
        var userId = current.UserId ?? throw new ForbiddenException("Authenticated user required.");
        var requests = uow.Repository<AssistanceRequest>();

        // Idempotency: a retried submission returns the original instead of creating a second request.
        var key = string.IsNullOrWhiteSpace(dto.IdempotencyKey) ? null : dto.IdempotencyKey.Trim();
        if (key is not null)
        {
            var existing = await requests.FirstOrDefaultAsync(r => r.RequesterId == userId && r.IdempotencyKey == key, ct);
            if (existing is not null)
                return new SubmitResultDto(await BuildDetailAsync(existing, false, ct), true);
        }

        var crisis = await uow.Repository<CrisisEvent>().GetByIdAsync(dto.CrisisEventId, ct)
                     ?? throw new NotFoundException("Crisis event not found.");
        if (crisis.Status == CrisisStatus.Closed)
            throw new ConflictException("This crisis event is closed and no longer accepts requests.");
        if (dto.Latitude.HasValue != dto.Longitude.HasValue)
            throw new AppValidationException("Latitude and longitude must be provided together.");

        // Location capture: an address without coordinates is looked up by the external map service. If that service is
        // unavailable the request is still accepted (without coordinates) and the history says so. This runs before
        // anything is staged because the mapping service saves its own lookup cache.
        var latitude = dto.Latitude;
        var longitude = dto.Longitude;
        string? locationNote = null;
        if (latitude is null && !string.IsNullOrWhiteSpace(dto.AddressText) && mapping is not null)
        {
            try
            {
                var located = await mapping.GeocodeAsync(dto.AddressText, ct);
                if (located is { Found: true, Latitude: not null, Longitude: not null })
                {
                    latitude = located.Latitude;
                    longitude = located.Longitude;
                    locationNote = $"Location worked out from the address ({located.Source}).";
                }
                else
                {
                    locationNote = "The address could not be located automatically; staff may need to contact the requester.";
                }
            }
            catch (AppValidationException)
            {
                locationNote = "The address could not be located automatically; staff may need to contact the requester.";
            }
        }

        var now = Now;
        var request = new AssistanceRequest
        {
            CrisisEventId = dto.CrisisEventId,
            RequesterId = userId,
            IdempotencyKey = key,
            Category = dto.Category,
            Description = dto.Description.Trim(),
            PeopleAffected = dto.PeopleAffected,
            Latitude = latitude,
            Longitude = longitude,
            AddressText = dto.AddressText,
            DeclaredUrgency = dto.DeclaredUrgency,
            Urgency = dto.DeclaredUrgency,
            Status = RequestStatus.Submitted,
            CreatedAtUtc = now
        };
        await requests.AddAsync(request, ct);
        await AddHistoryAsync(request.Id, "Submitted", null, RequestStatus.Submitted, null, true, ct);
        if (locationNote is not null)
            await AddHistoryAsync(request.Id, "LocationChecked", null, null, locationNote, false, ct);

        // AI recommendation (falls back automatically if the AI service is unavailable)
        var analysis = await analyzer.AnalyzeAsync(ToInput(request), ct);
        await CreateRecommendationAsync(request, analysis, ct);

        // Duplicate detection: flag only, never delete.
        var since = now - DuplicateWindow;
        var candidates = await requests.ListAsync(
            r => r.CrisisEventId == dto.CrisisEventId && r.Id != request.Id
                 && r.Status != RequestStatus.Closed && r.Status != RequestStatus.Merged
                 && r.CreatedAtUtc >= since,
            q => q.OrderByDescending(r => r.CreatedAtUtc), take: 200, ct: ct);
        var matches = detector.Find(request, candidates);
        foreach (var m in matches)
        {
            await uow.Repository<DuplicateLink>().AddAsync(new DuplicateLink
            {
                RequestId = request.Id,
                DuplicateOfRequestId = m.DuplicateOfRequestId,
                SimilarityScore = m.Score,
                Reason = m.Reason,
                CreatedAtUtc = now
            }, ct);
        }
        if (matches.Count > 0)
        {
            await AddHistoryAsync(request.Id, "DuplicatesFlagged", null, null,
                $"{matches.Count} possible duplicate(s) flagged for human review.", false, ct);
            await audit.RecordAsync("DuplicatesFlagged", nameof(AssistanceRequest), request.Id.ToString(),
                JsonSerializer.Serialize(matches), ct: ct);
        }

        request.Status = RequestStatus.UnderReview;
        await AddHistoryAsync(request.Id, "UnderReview", RequestStatus.Submitted, RequestStatus.UnderReview,
            null, true, ct);
        await Events.PublishAsync(new RequestStatusChanged(
            Guid.NewGuid(), request.Id, request.RequesterId, RequestStatus.Submitted, RequestStatus.UnderReview), ct);
        await audit.RecordAsync("RequestSubmitted", nameof(AssistanceRequest), request.Id.ToString(),
            $"Category={request.Category}; DeclaredUrgency={request.DeclaredUrgency}; People={request.PeopleAffected}", ct: ct);

        await uow.SaveChangesAsync(ct);
        return new SubmitResultDto(await BuildDetailAsync(request, false, ct), false);
    }

    // =====================================================================
    // Queries
    // =====================================================================
    public async Task<RequestPage> GetMineAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var userId = current.UserId ?? throw new ForbiddenException("Authenticated user required.");
        (page, pageSize) = Normalize(page, pageSize);
        var repo = uow.Repository<AssistanceRequest>();
        var total = await repo.CountAsync(r => r.RequesterId == userId, ct);
        var items = await repo.ListAsync(r => r.RequesterId == userId,
            q => q.OrderByDescending(r => r.CreatedAtUtc), (page - 1) * pageSize, pageSize, ct);
        return new RequestPage(items.Select(r => ToDto(r, false)).ToList(), total, page, pageSize);
    }

    public async Task<RequestPage> ListAsync(RequestFilter filter, int page, int pageSize, CancellationToken ct = default)
    {
        RequireStaff();
        (page, pageSize) = Normalize(page, pageSize);
        var crisisId = filter.CrisisEventId;
        var status = filter.Status;
        var urgency = filter.Urgency;
        var category = filter.Category;
        Expression<Func<AssistanceRequest, bool>> where = r =>
            (crisisId == null || r.CrisisEventId == crisisId) &&
            (status == null || r.Status == status) &&
            (urgency == null || r.Urgency == urgency) &&
            (category == null || r.Category == category);

        var repo = uow.Repository<AssistanceRequest>();
        var total = await repo.CountAsync(where, ct);
        // Highest priority first; oldest first among equals.
        var items = await repo.ListAsync(where,
            q => q.OrderByDescending(r => r.PriorityScore).ThenBy(r => r.CreatedAtUtc),
            (page - 1) * pageSize, pageSize, ct);
        return new RequestPage(items.Select(r => ToDto(r, true)).ToList(), total, page, pageSize);
    }

    public async Task<RequestDetailDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var request = await LoadAsync(id, ct);
        var staff = IsStaff();
        // Do not reveal that other people's requests exist.
        if (!staff && request.RequesterId != current.UserId)
            throw new NotFoundException("Request not found.");
        return await BuildDetailAsync(request, staff, ct);
    }

    // =====================================================================
    // AI recommendation (staff can request a fresh analysis)
    // =====================================================================
    public async Task<RequestDetailDto> AnalyzeAsync(Guid id, CancellationToken ct = default)
    {
        RequireStaff();
        var request = await LoadAsync(id, ct);
        EnsureOpen(request);

        await SupersedeOpenRecommendationsAsync(id, ct);
        var analysis = await analyzer.AnalyzeAsync(ToInput(request), ct);
        await CreateRecommendationAsync(request, analysis, ct);
        request.UpdatedAtUtc = Now;
        uow.Repository<AssistanceRequest>().Update(request);

        await uow.SaveChangesAsync(ct);
        return await BuildDetailAsync(request, true, ct);
    }

    // =====================================================================
    // Human-in-the-loop review
    // =====================================================================
    public async Task<RequestDetailDto> ReviewAsync(Guid id, ReviewRequestDto dto, CancellationToken ct = default)
    {
        RequireStaff();
        var request = await LoadAsync(id, ct);
        EnsureOpen(request);

        var rec = await GetReviewableRecommendationAsync(id, ct)
                  ?? throw new ConflictException("There is no pending AI recommendation for this request. Run the analysis again first.");

        var note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
        RequestCategory finalCategory;
        UrgencyLevel finalUrgency;
        RecommendationStatus newRecStatus;
        var newRequestStatus = request.Status;

        switch (dto.Decision)
        {
            case ReviewAction.Approve:
                finalCategory = rec.SuggestedCategory;
                finalUrgency = rec.SuggestedUrgency;
                newRecStatus = RecommendationStatus.Approved;
                newRequestStatus = RequestStatus.Triaged;
                break;

            case ReviewAction.Modify:
                if (dto.FinalCategory is null && dto.FinalUrgency is null)
                    throw new AppValidationException("Modify requires a final category and/or a final urgency.");
                if (note is null)
                    throw new AppValidationException("A reason (note) is required when overriding the AI recommendation.");
                finalCategory = dto.FinalCategory ?? rec.SuggestedCategory;
                finalUrgency = dto.FinalUrgency ?? rec.SuggestedUrgency;
                newRecStatus = RecommendationStatus.Modified;
                newRequestStatus = RequestStatus.Triaged;
                break;

            case ReviewAction.Reject:
                if (note is null)
                    throw new AppValidationException("A reason (note) is required when rejecting the AI recommendation.");
                finalCategory = request.Category;
                finalUrgency = request.Urgency;
                newRecStatus = RecommendationStatus.Rejected;
                break;

            case ReviewAction.Defer:
                finalCategory = request.Category;
                finalUrgency = request.Urgency;
                newRecStatus = RecommendationStatus.Deferred;
                break;

            default:
                throw new AppValidationException("Unknown review decision.");
        }

        // Role-appropriate approval for critical actions.
        var isCoordinator = current.Role == Roles.EmergencyCoordinator;
        var changesRequest = dto.Decision is ReviewAction.Approve or ReviewAction.Modify;
        if (changesRequest && finalUrgency == UrgencyLevel.Critical && !isCoordinator)
            throw new ForbiddenException("Critical priority decisions require an Emergency Coordinator.");
        if (request.Status == RequestStatus.Escalated && !isCoordinator)
            throw new ForbiddenException("Escalated requests can only be decided by an Emergency Coordinator.");

        var now = Now;
        var previousStatus = request.Status;

        // The Suggested* fields stay untouched: the original recommendation is preserved.
        rec.Status = newRecStatus;
        rec.HumanAction = dto.Decision;
        rec.FinalCategory = finalCategory;
        rec.FinalUrgency = finalUrgency;
        rec.DecidedByUserId = current.UserId;
        rec.DecidedByEmail = current.Email;
        rec.DecidedAtUtc = now;
        rec.DecisionNote = note;
        rec.UpdatedAtUtc = now;
        uow.Repository<AiRecommendation>().Update(rec);

        if (changesRequest)
        {
            request.Category = finalCategory;
            request.Urgency = finalUrgency;
            request.PriorityScore = finalUrgency == rec.SuggestedUrgency
                ? rec.PriorityScore
                : PriorityScoring.BaseScore(finalUrgency);
        }
        request.Status = newRequestStatus;
        request.UpdatedAtUtc = now;
        uow.Repository<AssistanceRequest>().Update(request);

        await AddHistoryAsync(request.Id, $"Recommendation{newRecStatus}", previousStatus, request.Status, note,
            changesRequest, ct);
        if (request.Status != previousStatus)
            await Events.PublishAsync(new RequestStatusChanged(
                Guid.NewGuid(), request.Id, request.RequesterId, previousStatus, request.Status), ct);

        // Audit record: recommendation, confidence, human action and timestamp (advanced requirement 2).
        var details = JsonSerializer.Serialize(new
        {
            recommendationId = rec.Id,
            suggested = new
            {
                category = rec.SuggestedCategory.ToString(),
                urgency = rec.SuggestedUrgency.ToString(),
                priorityScore = rec.PriorityScore,
                confidence = rec.Confidence,
                model = rec.ModelName,
                usedFallback = rec.UsedFallback
            },
            humanAction = dto.Decision.ToString(),
            final = new { category = finalCategory.ToString(), urgency = finalUrgency.ToString() },
            note,
            decidedAtUtc = now
        });
        await audit.RecordAsync($"AiDecision{newRecStatus}", nameof(AssistanceRequest), request.Id.ToString(), details, ct: ct);

        await uow.SaveChangesAsync(ct);
        return await BuildDetailAsync(request, true, ct);
    }

    public async Task<RequestDetailDto> EscalateAsync(Guid id, string? note, CancellationToken ct = default)
    {
        RequireStaff();
        var request = await LoadAsync(id, ct);
        EnsureOpen(request);
        if (request.Status == RequestStatus.Escalated)
            throw new ConflictException("Request is already escalated.");

        var previous = request.Status;
        request.Status = RequestStatus.Escalated;
        request.UpdatedAtUtc = Now;
        uow.Repository<AssistanceRequest>().Update(request);

        var cleanNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        await AddHistoryAsync(id, "Escalated", previous, RequestStatus.Escalated, cleanNote, false, ct);
        await Events.PublishAsync(new RequestStatusChanged(
            Guid.NewGuid(), request.Id, request.RequesterId, previous, RequestStatus.Escalated), ct);
        await audit.RecordAsync("RequestEscalated", nameof(AssistanceRequest), id.ToString(), cleanNote, ct: ct);
        await uow.SaveChangesAsync(ct);
        return await BuildDetailAsync(request, true, ct);
    }

    // =====================================================================
    // Duplicates: merge or dismiss (nothing is ever deleted)
    // =====================================================================
    public async Task<RequestDetailDto> MergeAsync(Guid id, MergeRequestDto dto, CancellationToken ct = default)
    {
        RequireStaff();
        if (dto.DuplicateOfRequestId == id)
            throw new AppValidationException("A request cannot be merged into itself.");

        var request = await LoadAsync(id, ct);
        var target = await LoadAsync(dto.DuplicateOfRequestId, ct);
        EnsureOpen(request);
        if (target.Status is RequestStatus.Closed or RequestStatus.Merged)
            throw new ConflictException($"The target request is already {target.Status}.");
        if (request.CrisisEventId != target.CrisisEventId)
            throw new ConflictException("Requests belong to different crisis events and cannot be merged.");

        var note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
        var previous = request.Status;
        var now = Now;

        request.Status = RequestStatus.Merged;
        request.MergedIntoRequestId = target.Id;
        request.UpdatedAtUtc = now;
        uow.Repository<AssistanceRequest>().Update(request);

        var links = await uow.Repository<DuplicateLink>().ListAsync(
            d => d.RequestId == id && d.DuplicateOfRequestId == target.Id, ct: ct);
        foreach (var link in links)
        {
            link.Status = DuplicateStatus.ConfirmedMerged;
            link.UpdatedAtUtc = now;
            uow.Repository<DuplicateLink>().Update(link);
        }
        await SupersedeOpenRecommendationsAsync(id, ct);

        await AddHistoryAsync(id, "MergedIntoAnotherRequest", previous, RequestStatus.Merged, note, true, ct);
        await AddHistoryAsync(target.Id, "DuplicateMergedIn", null, null, $"Request {id} merged into this one.", false, ct);
        await Events.PublishAsync(new RequestStatusChanged(
            Guid.NewGuid(), request.Id, request.RequesterId, previous, RequestStatus.Merged), ct);
        await audit.RecordAsync("RequestMerged", nameof(AssistanceRequest), id.ToString(),
            JsonSerializer.Serialize(new { mergedRequestId = id, intoRequestId = target.Id, note }), ct: ct);

        await uow.SaveChangesAsync(ct);
        return await BuildDetailAsync(request, true, ct);
    }

    public async Task<RequestDetailDto> DismissDuplicateAsync(Guid id, Guid linkId, CancellationToken ct = default)
    {
        RequireStaff();
        var request = await LoadAsync(id, ct);
        var link = await uow.Repository<DuplicateLink>().GetByIdAsync(linkId, ct);
        if (link is null || link.RequestId != id)
            throw new NotFoundException("Duplicate flag not found for this request.");

        link.Status = DuplicateStatus.Dismissed;
        link.UpdatedAtUtc = Now;
        uow.Repository<DuplicateLink>().Update(link);

        await AddHistoryAsync(id, "DuplicateDismissed", null, null,
            $"Not a duplicate of {link.DuplicateOfRequestId}.", false, ct);
        await audit.RecordAsync("DuplicateDismissed", nameof(AssistanceRequest), id.ToString(),
            link.DuplicateOfRequestId.ToString(), ct: ct);
        await uow.SaveChangesAsync(ct);
        return await BuildDetailAsync(request, true, ct);
    }

    public async Task<RequestDetailDto> CloseAsync(Guid id, string? note, CancellationToken ct = default)
    {
        RequireStaff();
        var request = await LoadAsync(id, ct);
        EnsureOpen(request);

        var previous = request.Status;
        var now = Now;
        request.Status = RequestStatus.Closed;
        request.ClosedAtUtc = now;
        request.UpdatedAtUtc = now;
        uow.Repository<AssistanceRequest>().Update(request);
        await SupersedeOpenRecommendationsAsync(id, ct);

        var cleanNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        await AddHistoryAsync(id, "Closed", previous, RequestStatus.Closed, cleanNote, true, ct);
        await Events.PublishAsync(new RequestStatusChanged(
            Guid.NewGuid(), request.Id, request.RequesterId, previous, RequestStatus.Closed), ct);
        await audit.RecordAsync("RequestClosed", nameof(AssistanceRequest), id.ToString(), cleanNote, ct: ct);
        await uow.SaveChangesAsync(ct);
        return await BuildDetailAsync(request, true, ct);
    }

    // =====================================================================
    // Helpers
    // =====================================================================
    private bool IsStaff() => current.Role is Roles.CaseManager or Roles.EmergencyCoordinator;

    private void RequireStaff()
    {
        if (!IsStaff())
            throw new ForbiddenException("Only case managers and emergency coordinators can perform this action.");
    }

    private static void EnsureOpen(AssistanceRequest r)
    {
        if (r.Status is RequestStatus.Closed or RequestStatus.Merged)
            throw new ConflictException($"Request is already {r.Status}.");
    }

    private static (int Page, int PageSize) Normalize(int page, int pageSize) =>
        (Math.Max(1, page), Math.Clamp(pageSize, 1, 100));

    private static AnalysisInput ToInput(AssistanceRequest r) =>
        new(r.Description, r.Category, r.DeclaredUrgency, r.PeopleAffected, r.Latitude.HasValue);

    private async Task<AssistanceRequest> LoadAsync(Guid id, CancellationToken ct) =>
        await uow.Repository<AssistanceRequest>().GetByIdAsync(id, ct)
        ?? throw new NotFoundException("Request not found.");

    private Task AddHistoryAsync(Guid requestId, string action, RequestStatus? from, RequestStatus? to,
        string? note, bool isPublic, CancellationToken ct) =>
        uow.Repository<RequestHistory>().AddAsync(new RequestHistory
        {
            RequestId = requestId,
            Action = action,
            FromStatus = from,
            ToStatus = to,
            ActorUserId = current.UserId,
            ActorEmail = current.Email,
            Note = note,
            IsPublic = isPublic,
            CreatedAtUtc = Now
        }, ct);

    private async Task CreateRecommendationAsync(AssistanceRequest request, AnalysisResult a, CancellationToken ct)
    {
        var rec = new AiRecommendation
        {
            RequestId = request.Id,
            SuggestedCategory = a.SuggestedCategory,
            SuggestedUrgency = a.SuggestedUrgency,
            PriorityScore = a.PriorityScore,
            Confidence = a.Confidence,
            Rationale = a.Rationale,
            ModelName = a.ModelName,
            UsedFallback = a.UsedFallback,
            Status = RecommendationStatus.Pending,
            CreatedAtUtc = Now
        };
        await uow.Repository<AiRecommendation>().AddAsync(rec, ct);
        request.PriorityScore = a.PriorityScore;

        await AddHistoryAsync(request.Id, "AiRecommendationGenerated", null, null,
            $"{a.ModelName}: {a.SuggestedCategory}/{a.SuggestedUrgency}, confidence {a.Confidence:0.00}", false, ct);
        await audit.RecordAsync("AiRecommendationGenerated", nameof(AssistanceRequest), request.Id.ToString(),
            JsonSerializer.Serialize(new
            {
                recommendationId = rec.Id,
                category = a.SuggestedCategory.ToString(),
                urgency = a.SuggestedUrgency.ToString(),
                priorityScore = a.PriorityScore,
                confidence = a.Confidence,
                model = a.ModelName,
                usedFallback = a.UsedFallback
            }), ct: ct);
    }

    private async Task<AiRecommendation?> GetReviewableRecommendationAsync(Guid requestId, CancellationToken ct)
    {
        var recs = await uow.Repository<AiRecommendation>().ListAsync(
            r => r.RequestId == requestId &&
                 (r.Status == RecommendationStatus.Pending || r.Status == RecommendationStatus.Deferred),
            q => q.OrderByDescending(r => r.CreatedAtUtc), take: 1, ct: ct);
        return recs.FirstOrDefault();
    }

    private async Task SupersedeOpenRecommendationsAsync(Guid requestId, CancellationToken ct)
    {
        var open = await uow.Repository<AiRecommendation>().ListAsync(
            r => r.RequestId == requestId &&
                 (r.Status == RecommendationStatus.Pending || r.Status == RecommendationStatus.Deferred), ct: ct);
        foreach (var r in open)
        {
            r.Status = RecommendationStatus.Superseded;
            r.UpdatedAtUtc = Now;
            uow.Repository<AiRecommendation>().Update(r);
        }
    }

    private async Task<RequestDetailDto> BuildDetailAsync(AssistanceRequest request, bool includeInternal, CancellationToken ct)
    {
        var history = await uow.Repository<RequestHistory>().ListAsync(
            h => h.RequestId == request.Id, q => q.OrderBy(h => h.CreatedAtUtc), ct: ct);
        var timeline = history
            .Where(h => includeInternal || h.IsPublic)
            .Select(h => new TimelineEntryDto(h.CreatedAtUtc, h.Action, h.FromStatus?.ToString(), h.ToStatus?.ToString(),
                includeInternal ? h.ActorEmail : null, includeInternal ? h.Note : null))
            .ToList();

        RecommendationDto? latest = null;
        IReadOnlyList<DuplicateDto> duplicates = Array.Empty<DuplicateDto>();
        if (includeInternal)
        {
            var recs = await uow.Repository<AiRecommendation>().ListAsync(
                r => r.RequestId == request.Id, q => q.OrderByDescending(r => r.CreatedAtUtc), take: 1, ct: ct);
            latest = recs.Count == 0 ? null : ToDto(recs[0]);

            var links = await uow.Repository<DuplicateLink>().ListAsync(
                d => d.RequestId == request.Id, q => q.OrderByDescending(d => d.SimilarityScore), ct: ct);
            duplicates = links.Select(l => ToDto(l)).ToList();
        }
        return new RequestDetailDto(ToDto(request, includeInternal), latest, duplicates, timeline);
    }

    private static RequestDto ToDto(AssistanceRequest r, bool includeInternal) => new(
        r.Id, r.CrisisEventId, r.RequesterId, r.Category.ToString(), r.Description, r.PeopleAffected,
        r.DeclaredUrgency.ToString(), r.Urgency.ToString(), r.Status.ToString(), r.Latitude, r.Longitude, r.AddressText,
        includeInternal ? r.PriorityScore : 0, r.MergedIntoRequestId, r.CreatedAtUtc, r.ClosedAtUtc);

    private static RecommendationDto ToDto(AiRecommendation r) => new(
        r.Id, r.SuggestedCategory.ToString(), r.SuggestedUrgency.ToString(), r.PriorityScore, r.Confidence,
        r.Rationale, r.ModelName, r.UsedFallback, r.Status.ToString(),
        r.HumanAction?.ToString(), r.FinalCategory?.ToString(), r.FinalUrgency?.ToString(),
        r.DecidedByUserId, r.DecidedByEmail, r.DecidedAtUtc, r.DecisionNote, r.CreatedAtUtc);

    private static DuplicateDto ToDto(DuplicateLink l) =>
        new(l.Id, l.DuplicateOfRequestId, l.SimilarityScore, l.Reason, l.Status.ToString());
}

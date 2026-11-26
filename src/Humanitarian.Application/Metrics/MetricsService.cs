using Humanitarian.Application.Common;
using Humanitarian.Application.Requests.Duplicates;
using Humanitarian.Domain.Entities;
using Humanitarian.Domain.Enums;

namespace Humanitarian.Application.Metrics;

public static class MetricsRoles
{
    public const string View = Roles.CaseManager + "," + Roles.EmergencyCoordinator + "," + Roles.SystemAdministrator;
}

/// <summary>How long a request may wait for help before it counts as overdue.</summary>
public static class SlaPolicy
{
    public static TimeSpan For(UrgencyLevel urgency) => urgency switch
    {
        UrgencyLevel.Critical => TimeSpan.FromHours(2),
        UrgencyLevel.High => TimeSpan.FromHours(6),
        UrgencyLevel.Medium => TimeSpan.FromHours(24),
        _ => TimeSpan.FromHours(72)
    };
}

public interface IMetricsService
{
    Task<OperationalMetricsDto> GetOperationalAsync(Guid? crisisEventId, CancellationToken ct = default);
}

/// <summary>
/// Operational KPIs for the coordination dashboard: unresolved and overdue cases, response times, resource utilisation,
/// shelter occupancy, AI override frequency, service coverage and message delivery health.
/// "Unresolved" = an open request that nobody has assigned help to yet (no resource allocation and no shelter placement).
/// The figures are calculated on demand from the live tables; for very large data a read model or SQL aggregates would
/// replace the in-memory calculation (documented as a scalability consideration).
/// </summary>
public class MetricsService(IUnitOfWork uow, ICurrentUser current, TimeProvider time) : IMetricsService
{
    private const double CoverageRadiusKm = 10;

    private static readonly RequestStatus[] OpenStatuses =
        [RequestStatus.Submitted, RequestStatus.UnderReview, RequestStatus.Triaged, RequestStatus.Escalated];

    public async Task<OperationalMetricsDto> GetOperationalAsync(Guid? crisisEventId, CancellationToken ct = default)
    {
        if (current.Role is not (Roles.CaseManager or Roles.EmergencyCoordinator or Roles.SystemAdministrator))
            throw new ForbiddenException("Only case managers, emergency coordinators and administrators can see operational metrics.");
        var now = time.GetUtcNow().UtcDateTime;

        // ---- load ----
        var requests = (await uow.Repository<AssistanceRequest>().ListAsync(ct: ct))
            .Where(r => crisisEventId == null || r.CrisisEventId == crisisEventId).ToList();
        var requestIds = requests.Select(r => r.Id).ToHashSet();
        var recs = (await uow.Repository<AiRecommendation>().ListAsync(ct: ct)).Where(r => requestIds.Contains(r.RequestId)).ToList();
        var resourceAllocs = (await uow.Repository<ResourceAllocation>().ListAsync(ct: ct)).Where(a => requestIds.Contains(a.RequestId)).ToList();
        var shelterAllocs = (await uow.Repository<ShelterAllocation>().ListAsync(ct: ct)).Where(a => requestIds.Contains(a.RequestId)).ToList();

        // ---- requests ----
        var assisted = resourceAllocs.Where(a => a.Status != AllocationStatus.Cancelled).Select(a => a.RequestId)
            .Concat(shelterAllocs.Select(a => a.RequestId)).ToHashSet();
        var open = requests.Where(r => OpenStatuses.Contains(r.Status)).ToList();
        var unresolved = open.Where(r => !assisted.Contains(r.Id)).ToList();
        var overdue = unresolved.Where(r => now - r.CreatedAtUtc > SlaPolicy.For(r.Urgency)).ToList();

        var requestMetrics = new RequestMetricsDto(
            requests.Count, open.Count, unresolved.Count, overdue.Count,
            requests.Count(r => r.Status == RequestStatus.Escalated), requests.Count(r => r.Status == RequestStatus.Closed),
            Enum.GetValues<RequestStatus>().ToDictionary(s => s.ToString(), s => requests.Count(r => r.Status == s)),
            Enum.GetValues<UrgencyLevel>().ToDictionary(u => u.ToString(), u => open.Count(r => r.Urgency == u)),
            Enum.GetValues<RequestCategory>().ToDictionary(c => c.ToString(), c => open.Count(r => r.Category == c)));

        // ---- response times ----
        var byId = requests.ToDictionary(r => r.Id);
        var waits = recs.Where(r => r.DecidedAtUtc != null)
            .GroupBy(r => r.RequestId)
            .Select(g => Math.Max(0, (g.Min(r => r.DecidedAtUtc!.Value) - byId[g.Key].CreatedAtUtc).TotalMinutes))
            .ToList();
        var resolutions = requests.Where(r => r.ClosedAtUtc != null)
            .Select(r => Math.Max(0, (r.ClosedAtUtc!.Value - r.CreatedAtUtc).TotalHours)).ToList();
        var responseTimes = new ResponseTimeDto(
            waits.Count,
            waits.Count == 0 ? null : Math.Round(waits.Average(), 1),
            resolutions.Count == 0 ? null : Math.Round(resolutions.Average(), 1));

        // ---- AI governance ----
        var decided = recs.Where(r => r.HumanAction != null).ToList();
        int Count(ReviewAction a) => decided.Count(r => r.HumanAction == a);
        var ai = new AiMetricsDto(
            recs.Count, decided.Count, Count(ReviewAction.Approve), Count(ReviewAction.Modify), Count(ReviewAction.Reject),
            Count(ReviewAction.Defer),
            Pct(Count(ReviewAction.Modify) + Count(ReviewAction.Reject), decided.Count),
            Pct(recs.Count(r => r.UsedFallback), recs.Count),
            recs.Count == 0 ? 0 : Math.Round(recs.Average(r => r.Confidence), 2));

        var workload = decided.Where(r => !string.IsNullOrEmpty(r.DecidedByEmail))
            .GroupBy(r => r.DecidedByEmail!)
            .Select(g => new StaffWorkloadDto(g.Key, g.Count(),
                g.Count(r => r.HumanAction is ReviewAction.Modify or ReviewAction.Reject)))
            .OrderByDescending(w => w.Decisions).Take(10).ToList();

        // ---- resources (stock is shared by every crisis) ----
        var items = (await uow.Repository<ResourceItem>().ListAsync(i => i.IsActive, ct: ct));
        var resources = Enum.GetValues<ResourceCategory>().Select(cat =>
        {
            var list = items.Where(i => i.Category == cat).ToList();
            var total = list.Sum(i => i.QuantityTotal);
            var available = list.Sum(i => i.QuantityAvailable);
            return new ResourceCategoryMetricsDto(cat.ToString(), list.Count, total, available,
                total == 0 ? 0 : Math.Round(100.0 * (total - available) / total, 1),
                list.Count(i => i.QuantityAvailable <= i.ReorderLevel));
        }).Where(r => r.Items > 0).ToList();

        // ---- shelters ----
        var shelters = (await uow.Repository<Shelter>().ListAsync(ct: ct))
            .Where(s => crisisEventId == null || s.CrisisEventId == null || s.CrisisEventId == crisisEventId).ToList();
        var openShelters = shelters.Where(s => s.Status == ShelterStatus.Open).ToList();
        var capacity = openShelters.Sum(s => s.Capacity);
        var occupancy = openShelters.Sum(s => s.Occupancy);
        var shelterMetrics = new ShelterMetricsDto(shelters.Count, openShelters.Count, capacity, occupancy,
            Pct(occupancy, capacity), openShelters.Count(s => s.Occupancy >= s.Capacity));

        // ---- volunteers ----
        var volunteers = await uow.Repository<Volunteer>().ListAsync(ct: ct);
        var tasks = (await uow.Repository<VolunteerTask>().ListAsync(ct: ct))
            .Where(t => crisisEventId == null || t.CrisisEventId == crisisEventId).ToList();
        var taskIds = tasks.Select(t => t.Id).ToHashSet();
        var assignments = (await uow.Repository<TaskAssignment>().ListAsync(ct: ct)).Where(a => taskIds.Contains(a.TaskId)).ToList();
        var accepted = assignments.Count(a => a.Status is AssignmentStatus.Accepted or AssignmentStatus.Completed);
        var declined = assignments.Count(a => a.Status == AssignmentStatus.Declined);
        var volunteerMetrics = new VolunteerMetricsDto(
            volunteers.Count, volunteers.Count(v => v.IsVerified), volunteers.Count(v => v.IsActive),
            tasks.Count(t => t.Status == VolunteerTaskStatus.Open), tasks.Count(t => t.Status == VolunteerTaskStatus.Filled),
            tasks.Count(t => t.Status == VolunteerTaskStatus.Completed),
            Pct(accepted, accepted + declined),
            Math.Round(assignments.Where(a => a.Status == AssignmentStatus.Completed).Sum(a => a.HoursWorked ?? 0), 1));

        // ---- service coverage: how many open requests are within reach of an open shelter ----
        var located = open.Where(r => r.Latitude.HasValue && r.Longitude.HasValue).ToList();
        var shelterPoints = openShelters.Where(s => s.Latitude.HasValue && s.Longitude.HasValue).ToList();
        var nearest = shelterPoints.Count == 0
            ? new List<double>()
            : located.Select(r => shelterPoints.Min(s => GeoMath.DistanceKm(r.Latitude!.Value, r.Longitude!.Value, s.Latitude!.Value, s.Longitude!.Value))).ToList();
        var coverage = new CoverageDto(located.Count, nearest.Count(d => d <= CoverageRadiusKm), Pct(nearest.Count(d => d <= CoverageRadiusKm), located.Count),
            nearest.Count == 0 ? null : Math.Round(nearest.Average(), 1), CoverageRadiusKm);

        // ---- message delivery health ----
        var deliveries = await uow.Repository<NotificationDelivery>().ListAsync(ct: ct);
        var deliveryMetrics = new DeliveryMetricsDto(
            deliveries.Count(d => d.Status is DeliveryStatus.Pending or DeliveryStatus.Processing),
            deliveries.Count(d => d.Status == DeliveryStatus.Sent),
            deliveries.Count(d => d.Status == DeliveryStatus.Failed));

        return new OperationalMetricsDto(now, crisisEventId, requestMetrics, responseTimes, ai, resources, shelterMetrics,
            volunteerMetrics, coverage, deliveryMetrics, workload);
    }

    private static double Pct(int part, int whole) => whole == 0 ? 0 : Math.Round(100.0 * part / whole, 1);
}

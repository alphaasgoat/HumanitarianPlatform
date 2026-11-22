using Humanitarian.Domain.Entities;

namespace Humanitarian.Application.Resources;

public record PlanTake(Guid ResourceItemId, string ResourceName, int Quantity);

public record PlanLine(AssistanceRequest Request, int Needed, int Proposed, IReadOnlyList<PlanTake> Takes)
{
    public string Outcome => Proposed >= Needed ? "Full" : Proposed > 0 ? "Partial" : "Unfulfilled";
}

public record ShortagePlan(int TotalNeeded, int TotalAvailable, IReadOnlyList<PlanLine> Lines)
{
    public int Shortfall => Math.Max(0, TotalNeeded - TotalAvailable);
    public bool HasShortage => TotalNeeded > TotalAvailable;
}

public interface IShortagePlanner
{
    ShortagePlan Plan(IEnumerable<AssistanceRequest> requests, IEnumerable<ResourceItem> resources,
        IReadOnlyDictionary<Guid, int> alreadyAllocated);
}

/// <summary>
/// Resolves several requests competing for the same limited stock. Order of service:
/// urgency (Critical first), then priority score, then oldest first. Planning works on a copy of the
/// stock and never changes real inventory; a human applies the plan.
/// </summary>
public class ShortagePlanner(IResourceMatcher matcher) : IShortagePlanner
{
    public ShortagePlan Plan(IEnumerable<AssistanceRequest> requests, IEnumerable<ResourceItem> resources,
        IReadOnlyDictionary<Guid, int> alreadyAllocated)
    {
        var active = resources.Where(r => r.IsActive).ToList();
        var stock = active.ToDictionary(r => r.Id, r => r.QuantityAvailable);
        var lines = new List<PlanLine>();
        var totalNeeded = 0;

        var ordered = requests
            .OrderByDescending(r => r.Urgency)
            .ThenByDescending(r => r.PriorityScore)
            .ThenBy(r => r.CreatedAtUtc)
            .ToList();

        foreach (var request in ordered)
        {
            alreadyAllocated.TryGetValue(request.Id, out var already);
            var needed = ResourceRules.EstimateQuantity(request) - already;
            if (needed <= 0) continue; // already fully served

            totalNeeded += needed;

            // Snapshots carry the stock that is still left after higher-priority requests were served.
            var snapshots = active.Where(r => stock[r.Id] > 0).Select(r => Snapshot(r, stock[r.Id])).ToList();
            var takes = new List<PlanTake>();
            var remaining = needed;
            foreach (var match in matcher.Rank(request, needed, snapshots))
            {
                if (remaining <= 0) break;
                var take = Math.Min(remaining, stock[match.Resource.Id]);
                if (take <= 0) continue;
                stock[match.Resource.Id] -= take;
                remaining -= take;
                takes.Add(new PlanTake(match.Resource.Id, match.Resource.Name, take));
            }

            lines.Add(new PlanLine(request, needed, needed - remaining, takes));
        }

        return new ShortagePlan(totalNeeded, active.Sum(r => r.QuantityAvailable), lines);
    }

    private static ResourceItem Snapshot(ResourceItem r, int available) => new()
    {
        Id = r.Id, Name = r.Name, Category = r.Category, Unit = r.Unit, QuantityTotal = r.QuantityTotal,
        QuantityAvailable = available, ReorderLevel = r.ReorderLevel, LocationName = r.LocationName,
        Latitude = r.Latitude, Longitude = r.Longitude, IsActive = r.IsActive, OrganisationId = r.OrganisationId
    };
}

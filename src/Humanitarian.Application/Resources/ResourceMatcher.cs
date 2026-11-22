using Humanitarian.Application.Requests.Duplicates;
using Humanitarian.Domain.Entities;
using Humanitarian.Domain.Enums;

namespace Humanitarian.Application.Resources;

public record ResourceMatch(ResourceItem Resource, double Score, double? DistanceKm, bool CanFullyCover, string Rationale);

/// <summary>Strategy interface: how candidate resources are ranked for a request.</summary>
public interface IResourceMatcher
{
    IReadOnlyList<ResourceMatch> Rank(AssistanceRequest request, int quantityNeeded, IEnumerable<ResourceItem> resources);
}

/// <summary>
/// Multi-constraint ranking (never just "nearest"):
///  hard constraints : right category, active, stock above zero
///  soft constraints : how much of the need it covers, distance, and stock left after allocating
/// For High/Critical requests speed matters, so proximity gets the largest weight.
/// </summary>
public class ResourceMatcher : IResourceMatcher
{
    public const double MaxDistanceKm = 100.0;

    public IReadOnlyList<ResourceMatch> Rank(AssistanceRequest request, int quantityNeeded, IEnumerable<ResourceItem> resources)
    {
        var wanted = ResourceRules.ResourceCategoryFor(request.Category);
        if (wanted is null) return Array.Empty<ResourceMatch>();

        var urgent = request.Urgency is UrgencyLevel.High or UrgencyLevel.Critical;
        var (wCoverage, wProximity, wStock) = urgent ? (0.35, 0.50, 0.15) : (0.45, 0.25, 0.30);
        var needed = Math.Max(1, quantityNeeded);

        var results = new List<ResourceMatch>();
        foreach (var r in resources)
        {
            if (!r.IsActive || r.Category != wanted || r.QuantityAvailable <= 0) continue;

            var coverage = Math.Min(1.0, (double)r.QuantityAvailable / needed);

            double? km = null;
            var proximity = 0.5; // neutral when a location is missing
            if (request.Latitude.HasValue && request.Longitude.HasValue && r.Latitude.HasValue && r.Longitude.HasValue)
            {
                km = GeoMath.DistanceKm(request.Latitude.Value, request.Longitude.Value, r.Latitude.Value, r.Longitude.Value);
                proximity = Math.Max(0.0, 1.0 - km.Value / MaxDistanceKm);
            }

            var remaining = r.QuantityAvailable - Math.Min(r.QuantityAvailable, needed);
            var stockHealth = r.ReorderLevel <= 0 ? 1.0 : Math.Min(1.0, (double)remaining / r.ReorderLevel);

            var score = Math.Round(100 * (wCoverage * coverage + wProximity * proximity + wStock * stockHealth), 1);
            var distanceText = km is null ? "distance unknown" : $"{km.Value:0.#} km away";
            var rationale =
                $"Covers {coverage * 100:0}% of the need; {distanceText}; {remaining} {r.Unit} left after allocation " +
                $"(reorder level {r.ReorderLevel}). Weighting: {(urgent ? "urgent, proximity first" : "standard, coverage and stock first")}.";

            results.Add(new ResourceMatch(r, score, km, r.QuantityAvailable >= needed, rationale));
        }

        return results
            .OrderByDescending(m => m.Score)
            .ThenBy(m => m.DistanceKm ?? double.MaxValue)
            .ToList();
    }
}

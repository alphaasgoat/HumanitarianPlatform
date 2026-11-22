using Humanitarian.Application.Requests.Duplicates;
using Humanitarian.Domain.Entities;
using Humanitarian.Domain.Enums;

namespace Humanitarian.Application.Shelters;

/// <summary>Special needs read from a request (wheelchair access, medical care).</summary>
public record ShelterNeeds(bool Wheelchair, bool Medical)
{
    private static readonly string[] WheelchairWords =
        { "wheelchair", "disabled", "disability", "bedridden", "mobility", "crutches", "elderly" };

    private static readonly string[] MedicalWords =
        { "injur", "medical", "insulin", "pregnan", "dialysis", "oxygen", "wound", "fever", "asthma", "sick" };

    public static ShelterNeeds From(AssistanceRequest request)
    {
        var text = (request.Description ?? string.Empty).ToLowerInvariant();
        return new ShelterNeeds(
            request.Category == RequestCategory.Accessibility || WheelchairWords.Any(w => text.Contains(w)),
            request.Category == RequestCategory.Medicine || MedicalWords.Any(w => text.Contains(w)));
    }
}

public record ShelterMatch(Shelter Shelter, double Score, double? DistanceKm, string Rationale, IReadOnlyList<string> Warnings);

/// <summary>Strategy interface: how shelters are ranked for a household.</summary>
public interface IShelterMatcher
{
    IReadOnlyList<ShelterMatch> Rank(AssistanceRequest request, int peopleCount, IEnumerable<Shelter> shelters);
}

/// <summary>
/// Hard constraints: open, enough free spaces, wheelchair access when needed.
/// Soft constraints (weights): distance 0.35, spare room left after placing 0.30,
/// medical support when needed 0.20, food and water on site 0.15.
/// </summary>
public class ShelterMatcher : IShelterMatcher
{
    public const double MaxDistanceKm = 100.0;

    public IReadOnlyList<ShelterMatch> Rank(AssistanceRequest request, int peopleCount, IEnumerable<Shelter> shelters)
    {
        var needs = ShelterNeeds.From(request);
        var people = Math.Max(1, peopleCount);
        var results = new List<ShelterMatch>();

        foreach (var s in shelters)
        {
            if (s.Status != ShelterStatus.Open || s.AvailableSpaces < people) continue;
            if (needs.Wheelchair && !s.WheelchairAccessible) continue;

            double? km = null;
            var proximity = 0.5; // neutral when a location is missing
            if (request.Latitude.HasValue && request.Longitude.HasValue && s.Latitude.HasValue && s.Longitude.HasValue)
            {
                km = GeoMath.DistanceKm(request.Latitude.Value, request.Longitude.Value, s.Latitude.Value, s.Longitude.Value);
                proximity = Math.Max(0.0, 1.0 - km.Value / MaxDistanceKm);
            }

            var spare = s.Capacity <= 0 ? 0.0 : (double)(s.AvailableSpaces - people) / s.Capacity;
            var medical = needs.Medical ? (s.HasMedicalSupport ? 1.0 : 0.0) : 1.0;
            var services = ((s.ProvidesFood ? 1 : 0) + (s.ProvidesWater ? 1 : 0)) / 2.0;

            var score = Math.Round(100 * (0.35 * proximity + 0.30 * spare + 0.20 * medical + 0.15 * services), 1);

            var warnings = new List<string>();
            if (needs.Medical && !s.HasMedicalSupport) warnings.Add("Medical care may be needed, but this shelter has no medical support on site.");
            if (!s.ProvidesFood) warnings.Add("No food service on site.");
            if (!s.ProvidesWater) warnings.Add("No water service on site.");

            var distanceText = km is null ? "distance unknown" : $"{km.Value:0.#} km away";
            var rationale = $"{s.AvailableSpaces} spaces free ({s.AvailableSpaces - people} left after placing {people}); {distanceText}; " +
                            $"wheelchair access: {(s.WheelchairAccessible ? "yes" : "no")}; medical support: {(s.HasMedicalSupport ? "yes" : "no")}.";

            results.Add(new ShelterMatch(s, score, km, rationale, warnings));
        }

        return results
            .OrderByDescending(m => m.Score)
            .ThenBy(m => m.DistanceKm ?? double.MaxValue)
            .ToList();
    }
}

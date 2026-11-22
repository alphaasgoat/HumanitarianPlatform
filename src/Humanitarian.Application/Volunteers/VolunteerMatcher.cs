using Humanitarian.Application.Requests.Duplicates;
using Humanitarian.Domain.Entities;
using Humanitarian.Domain.Enums;

namespace Humanitarian.Application.Volunteers;

public record BusyWindow(DateTime StartUtc, DateTime EndUtc);

/// <summary>A volunteer together with the facts needed to judge a task: free time and existing commitments.</summary>
public record VolunteerCandidate(Volunteer Volunteer, IReadOnlyList<VolunteerAvailability> Availability, IReadOnlyList<BusyWindow> BusyWindows)
{
    public int ActiveAssignments => BusyWindows.Count;
}

public record VolunteerMatch(Volunteer Volunteer, double Score, double? DistanceKm, string Rationale);

/// <summary>Strategy interface: how volunteers are ranked for a task.</summary>
public interface IVolunteerMatcher
{
    IReadOnlyList<VolunteerMatch> Rank(VolunteerTask task, IEnumerable<VolunteerCandidate> candidates);
}

/// <summary>
/// Hard constraints: active, verified, has the required skill, free for the WHOLE shift, no clash with another task.
/// Soft constraints: skill fit 0.40, distance 0.35, workload fairness 0.25 (fewer active tasks is better).
/// </summary>
public class VolunteerMatcher : IVolunteerMatcher
{
    public const double MaxDistanceKm = 100.0;

    public static bool HasSkill(Volunteer v, VolunteerSkill required) =>
        required == VolunteerSkill.General || SkillSet.Parse(v.Skills).Contains(required);

    public static bool IsAvailable(VolunteerCandidate c, VolunteerTask t) =>
        c.Availability.Any(a => a.StartUtc <= t.StartUtc && a.EndUtc >= t.EndUtc);

    public static bool IsBusy(VolunteerCandidate c, VolunteerTask t) =>
        c.BusyWindows.Any(b => b.StartUtc < t.EndUtc && t.StartUtc < b.EndUtc);

    public IReadOnlyList<VolunteerMatch> Rank(VolunteerTask task, IEnumerable<VolunteerCandidate> candidates)
    {
        var results = new List<VolunteerMatch>();
        foreach (var c in candidates)
        {
            var v = c.Volunteer;
            if (!v.IsActive || !v.IsVerified) continue;
            if (!HasSkill(v, task.RequiredSkill)) continue;
            if (!IsAvailable(c, task) || IsBusy(c, task)) continue;

            var skillFit = task.RequiredSkill == VolunteerSkill.General ? 0.5 : 1.0;

            double? km = null;
            var proximity = 0.5;
            if (task.Latitude.HasValue && task.Longitude.HasValue && v.Latitude.HasValue && v.Longitude.HasValue)
            {
                km = GeoMath.DistanceKm(task.Latitude.Value, task.Longitude.Value, v.Latitude.Value, v.Longitude.Value);
                proximity = Math.Max(0.0, 1.0 - km.Value / MaxDistanceKm);
            }

            var fairness = 1.0 / (1 + c.ActiveAssignments);
            var score = Math.Round(100 * (0.40 * skillFit + 0.35 * proximity + 0.25 * fairness), 1);

            var distanceText = km is null ? "distance unknown" : $"{km.Value:0.#} km away";
            var rationale = $"Skill: {(task.RequiredSkill == VolunteerSkill.General ? "no special skill needed" : "has " + task.RequiredSkill)}; " +
                            $"free for the whole shift; {distanceText}; {c.ActiveAssignments} other active task(s).";
            results.Add(new VolunteerMatch(v, score, km, rationale));
        }

        return results
            .OrderByDescending(m => m.Score)
            .ThenBy(m => m.DistanceKm ?? double.MaxValue)
            .ToList();
    }
}

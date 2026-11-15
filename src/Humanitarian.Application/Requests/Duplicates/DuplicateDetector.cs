using System.Text.RegularExpressions;
using Humanitarian.Domain.Entities;

namespace Humanitarian.Application.Requests.Duplicates;

public record DuplicateMatch(Guid DuplicateOfRequestId, double Score, string Reason);

public interface IDuplicateDetector
{
    IReadOnlyList<DuplicateMatch> Find(AssistanceRequest target, IEnumerable<AssistanceRequest> candidates);
}

/// <summary>
/// Scores how likely two requests are the same need. It only SUGGESTS matches; a human decides.
/// score = 0.55 x text similarity (Jaccard) + 0.15 same category + 0.10 same requester + up to 0.20 closeness.
/// </summary>
public class DuplicateDetector : IDuplicateDetector
{
    public const double Threshold = 0.55;

    private static readonly HashSet<string> StopWords = new()
    {
        "the", "and", "for", "with", "that", "this", "are", "was", "have", "has", "our", "need", "needs",
        "please", "help", "from", "there", "they", "them", "been", "very", "you", "your", "can", "any"
    };

    public IReadOnlyList<DuplicateMatch> Find(AssistanceRequest target, IEnumerable<AssistanceRequest> candidates)
    {
        var targetTokens = Tokenize(target.Description);
        var matches = new List<DuplicateMatch>();

        foreach (var c in candidates)
        {
            if (c.Id == target.Id) continue;

            var jaccard = Jaccard(targetTokens, Tokenize(c.Description));
            var score = 0.55 * jaccard;
            var reasons = new List<string> { $"text similarity {jaccard:P0}" };

            if (target.Category == c.Category) { score += 0.15; reasons.Add("same category"); }
            if (target.RequesterId == c.RequesterId) { score += 0.10; reasons.Add("same requester"); }

            if (target.Latitude.HasValue && target.Longitude.HasValue && c.Latitude.HasValue && c.Longitude.HasValue)
            {
                var km = GeoMath.DistanceKm(target.Latitude.Value, target.Longitude.Value, c.Latitude.Value, c.Longitude.Value);
                if (km <= 0.5) { score += 0.20; reasons.Add($"{km:0.00} km apart"); }
                else if (km <= 2.0) { score += 0.10; reasons.Add($"{km:0.00} km apart"); }
            }

            score = Math.Round(Math.Min(1.0, score), 2);
            if (score >= Threshold)
                matches.Add(new DuplicateMatch(c.Id, score, string.Join(", ", reasons)));
        }

        return matches.OrderByDescending(m => m.Score).Take(5).ToList();
    }

    private static HashSet<string> Tokenize(string? text) =>
        Regex.Split((text ?? string.Empty).ToLowerInvariant(), @"[^\p{L}\p{N}]+")
            .Where(t => t.Length > 2 && !StopWords.Contains(t))
            .ToHashSet();

    private static double Jaccard(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0;
        var intersection = a.Count(b.Contains);
        var union = a.Count + b.Count - intersection;
        return union == 0 ? 0 : (double)intersection / union;
    }
}

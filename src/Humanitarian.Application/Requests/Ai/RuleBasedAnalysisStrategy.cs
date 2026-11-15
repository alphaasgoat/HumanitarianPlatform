using Humanitarian.Domain.Enums;

namespace Humanitarian.Application.Requests.Ai;

/// <summary>
/// Local, deterministic analyser. Works offline, so the platform keeps working when an external
/// AI service is unavailable. Every score component is written into the rationale (explainable).
/// </summary>
public class RuleBasedAnalysisStrategy : IAnalysisStrategy
{
    public string Name => "rule-based-v1";

    private sealed record CategoryRule(RequestCategory Category, string[] Keywords);

    // Order matters: on a tie the earlier rule wins, so life-saving categories come first.
    private static readonly CategoryRule[] CategoryRules =
    {
        new(RequestCategory.Rescue, new[] { "trapped", "rescue", "drowning", "stuck", "swept away", "buried", "cannot get out" }),
        new(RequestCategory.Medicine, new[] { "medicine", "medication", "medical", "insulin", "doctor", "injur", "bleed", "pregnan", "asthma", "oxygen", "dialysis", "wound", "fever", "fracture", "unconscious" }),
        new(RequestCategory.Water, new[] { "drinking water", "clean water", "thirst", "no water", "water supply", "bottled water" }),
        new(RequestCategory.Food, new[] { "food", "hungry", "hunger", "starv", "rice", "milk powder", "baby food", "meal", "ration" }),
        new(RequestCategory.Shelter, new[] { "shelter", "homeless", "roof", "collapsed", "evacuat", "displaced", "camp", "nowhere to stay", "house destroyed", "house is flooded" }),
        new(RequestCategory.Transportation, new[] { "transport", "vehicle", "stranded", "road blocked", "pickup", "ambulance", "need a ride", "boat" }),
        new(RequestCategory.Accessibility, new[] { "wheelchair", "disabled", "disability", "elderly", "blind", "bedridden", "mobility", "crutches" }),
    };

    private static readonly string[] LifeThreatKeywords =
    {
        "trapped", "drowning", "bleeding", "unconscious", "not breathing", "life-threatening",
        "life threatening", "buried", "swept away", "heart attack", "severe injury"
    };

    private static readonly string[] VulnerableKeywords =
    {
        "infant", "baby", "newborn", "child", "children", "elderly", "pregnan", "disabled", "wheelchair", "bedridden", "sick"
    };

    public Task<AnalysisResult> AnalyzeAsync(AnalysisInput input, CancellationToken ct = default)
    {
        var text = (input.Description ?? string.Empty).ToLowerInvariant();
        var reasons = new List<string>();

        // 1. Category
        var category = input.DeclaredCategory;
        var confidence = 0.40;
        CategoryRule? best = null;
        var bestHits = 0;
        var bestMatched = new List<string>();
        foreach (var rule in CategoryRules)
        {
            var matched = rule.Keywords.Where(k => text.Contains(k)).ToList();
            if (matched.Count > bestHits)
            {
                best = rule;
                bestHits = matched.Count;
                bestMatched = matched;
            }
        }

        if (best is not null)
        {
            category = best.Category;
            confidence = Math.Min(0.90, 0.55 + 0.10 * bestHits);
            reasons.Add($"Text matches {category} keywords ({string.Join(", ", bestMatched)}).");
            if (category != input.DeclaredCategory)
            {
                confidence -= 0.10;
                reasons.Add($"This differs from the category declared by the requester ({input.DeclaredCategory}).");
            }
        }
        else
        {
            reasons.Add("No category keywords found, so the declared category was kept.");
        }

        // 2. Priority score
        var score = PriorityScoring.BaseScore(input.DeclaredUrgency);
        reasons.Add($"Declared urgency {input.DeclaredUrgency} (base score {score}).");

        var lifeThreats = LifeThreatKeywords.Where(k => text.Contains(k)).ToList();
        if (lifeThreats.Count > 0)
        {
            score += 20;
            confidence = Math.Min(0.95, confidence + 0.05);
            reasons.Add($"Life-threat indicators: {string.Join(", ", lifeThreats)} (+20).");
        }

        var vulnerable = VulnerableKeywords.Where(k => text.Contains(k)).ToList();
        if (vulnerable.Count > 0)
        {
            score += 8;
            reasons.Add($"Vulnerable people mentioned: {string.Join(", ", vulnerable)} (+8).");
        }

        if (input.PeopleAffected >= 50) { score += 10; reasons.Add($"{input.PeopleAffected} people affected (+10)."); }
        else if (input.PeopleAffected >= 10) { score += 6; reasons.Add($"{input.PeopleAffected} people affected (+6)."); }
        else if (input.PeopleAffected >= 4) { score += 3; reasons.Add($"{input.PeopleAffected} people affected (+3)."); }

        if (!input.HasLocation)
            reasons.Add("No location was provided; responders may need to contact the requester.");

        score = Math.Clamp(score, 0, 100);
        var urgency = PriorityScoring.ToUrgency(score);
        confidence = Math.Round(Math.Clamp(confidence, 0.05, 0.95), 2);

        return Task.FromResult(new AnalysisResult(
            category, urgency, score, confidence, string.Join(" ", reasons), Name));
    }
}

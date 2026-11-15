namespace Humanitarian.Application.Requests.Ai;

/// <summary>
/// Tries each registered strategy in order, with a timeout per strategy. If one fails or times out
/// the next one is used; if all fail a safe "manual review required" result is returned. The
/// failure is recorded in the rationale so reviewers can see that a fallback was used.
/// </summary>
public class ResilientRequestAnalyzer(IEnumerable<IAnalysisStrategy> strategies) : IRequestAnalyzer
{
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);

    public async Task<AnalysisResult> AnalyzeAsync(AnalysisInput input, CancellationToken ct = default)
    {
        var failures = new List<string>();

        foreach (var strategy in strategies)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(Timeout);
                var result = await strategy.AnalyzeAsync(input, cts.Token);

                if (failures.Count == 0) return result;
                return result with
                {
                    UsedFallback = true,
                    Rationale = result.Rationale + " [Fallback used: " + string.Join("; ", failures) + "]"
                };
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw; // the caller cancelled; do not hide that
            }
            catch (Exception ex)
            {
                failures.Add($"{strategy.Name} failed ({ex.GetType().Name})");
            }
        }

        var reason = failures.Count == 0 ? "no analyser configured" : string.Join("; ", failures);
        return new AnalysisResult(
            input.DeclaredCategory, input.DeclaredUrgency, PriorityScoring.BaseScore(input.DeclaredUrgency),
            0.0, $"AI analysis unavailable ({reason}). Manual review required.", "manual-fallback", true);
    }
}

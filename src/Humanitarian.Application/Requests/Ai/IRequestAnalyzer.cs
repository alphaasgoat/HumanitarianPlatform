using Humanitarian.Domain.Enums;

namespace Humanitarian.Application.Requests.Ai;

public record AnalysisInput(
    string Description, RequestCategory DeclaredCategory, UrgencyLevel DeclaredUrgency,
    int PeopleAffected, bool HasLocation);

public record AnalysisResult(
    RequestCategory SuggestedCategory, UrgencyLevel SuggestedUrgency, int PriorityScore,
    double Confidence, string Rationale, string ModelName, bool UsedFallback = false);

/// <summary>Strategy pattern: one way of analysing a request (rule-based, LLM, ...).</summary>
public interface IAnalysisStrategy
{
    string Name { get; }
    Task<AnalysisResult> AnalyzeAsync(AnalysisInput input, CancellationToken ct = default);
}

/// <summary>What the rest of the system uses. Hides which strategy actually produced the result.</summary>
public interface IRequestAnalyzer
{
    Task<AnalysisResult> AnalyzeAsync(AnalysisInput input, CancellationToken ct = default);
}

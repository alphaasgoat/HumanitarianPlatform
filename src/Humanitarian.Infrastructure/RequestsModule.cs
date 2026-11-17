using Humanitarian.Application.Requests;
using Humanitarian.Application.Requests.Ai;
using Humanitarian.Application.Requests.Duplicates;
using Microsoft.Extensions.DependencyInjection;

namespace Humanitarian.Infrastructure;

/// <summary>Registers everything the assistance-request module needs (modular monolith: one module, one line).</summary>
public static class RequestsModule
{
    public static IServiceCollection AddRequestsModule(this IServiceCollection services)
    {
        // Strategy pattern: registration order = priority. In Phase 4 an LLM-backed strategy is
        // registered BEFORE the rule-based one; the rule-based one then acts as the fallback.
        services.AddScoped<IAnalysisStrategy, RuleBasedAnalysisStrategy>();
        services.AddScoped<IRequestAnalyzer, ResilientRequestAnalyzer>();

        services.AddSingleton<IDuplicateDetector, DuplicateDetector>();
        services.AddScoped<IAssistanceRequestService, AssistanceRequestService>();
        return services;
    }
}

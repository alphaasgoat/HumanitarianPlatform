using Humanitarian.Application.Metrics;
using Microsoft.Extensions.DependencyInjection;

namespace Humanitarian.Infrastructure;

public class MetricsModule : IPlatformModule
{
    public void Register(IServiceCollection services) => services.AddScoped<IMetricsService, MetricsService>();
}

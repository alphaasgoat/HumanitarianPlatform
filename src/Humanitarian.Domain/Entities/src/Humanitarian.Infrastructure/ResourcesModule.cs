using Humanitarian.Application.Resources;
using Microsoft.Extensions.DependencyInjection;

namespace Humanitarian.Infrastructure;

public class ResourcesModule : IPlatformModule
{
    public void Register(IServiceCollection services)
    {
        // Strategy: the matcher can be replaced (e.g. by an optimisation-based one) without touching the services.
        services.AddSingleton<IResourceMatcher, ResourceMatcher>();
        services.AddSingleton<IShortagePlanner, ShortagePlanner>();
        services.AddScoped<IResourceService, ResourceService>();
        services.AddScoped<IAllocationService, AllocationService>();
    }
}

using Humanitarian.Application.Mapping;
using Humanitarian.Infrastructure.Mapping;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Humanitarian.Infrastructure;

/// <summary>Mapping: external provider adapters (geocoding, routing) behind ports, with fallbacks in MappingService.</summary>
public class MappingModule : IPlatformModule
{
    public void Register(IServiceCollection services)
    {
        services.AddOptions<MappingSettings>().BindConfiguration(MappingSettings.Section);
        services.AddSingleton<IGeocodingProvider>(sp =>
            new NominatimGeocodingProvider(ExternalHttp.Client, sp.GetRequiredService<IOptions<MappingSettings>>()));
        services.AddSingleton<IRoutingProvider>(sp =>
            new OsrmRoutingProvider(ExternalHttp.Client, sp.GetRequiredService<IOptions<MappingSettings>>()));
        services.AddScoped<IMappingService, MappingService>();
    }
}

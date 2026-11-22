using Humanitarian.Application.Shelters;
using Microsoft.Extensions.DependencyInjection;

namespace Humanitarian.Infrastructure;

public class SheltersModule : IPlatformModule
{
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<IShelterMatcher, ShelterMatcher>();
        services.AddScoped<IShelterService, ShelterService>();
    }
}

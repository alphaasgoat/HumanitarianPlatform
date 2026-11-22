using Humanitarian.Application.Volunteers;
using Microsoft.Extensions.DependencyInjection;

namespace Humanitarian.Infrastructure;

public class VolunteersModule : IPlatformModule
{
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<IVolunteerMatcher, VolunteerMatcher>();
        services.AddScoped<IVolunteerService, VolunteerService>();
        services.AddScoped<IVolunteerTaskService, VolunteerTaskService>();
    }
}

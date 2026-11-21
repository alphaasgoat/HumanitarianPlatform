using Microsoft.Extensions.DependencyInjection;

namespace Humanitarian.Infrastructure;

/// <summary>
/// A feature module (resources, shelters, volunteers, ...) registers its own services.
/// Modules are discovered automatically, so a new module is just one new class and no shared file is edited.
/// </summary>
public interface IPlatformModule
{
    void Register(IServiceCollection services);
}

public static class PlatformModules
{
    public static IServiceCollection AddPlatformModules(this IServiceCollection services)
    {
        var moduleTypes = typeof(PlatformModules).Assembly.GetTypes()
            .Where(t => typeof(IPlatformModule).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract);

        foreach (var type in moduleTypes)
            ((IPlatformModule)Activator.CreateInstance(type)!).Register(services);

        return services;
    }
}

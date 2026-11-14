using Humanitarian.Application.Audit;
using Humanitarian.Application.Auth;
using Humanitarian.Application.Common;
using Humanitarian.Application.Crisis;
using Humanitarian.Application.Users;
using Humanitarian.Infrastructure.Persistence;
using Humanitarian.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Humanitarian.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Dependency Injection composition root for the Application + Infrastructure layers.</summary>
    public static IServiceCollection AddPlatformServices(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<JwtSettings>(config.GetSection(JwtSettings.Section));
        services.AddSingleton(TimeProvider.System);

        services.AddDbContext<AppDbContext>(o =>
            o.UseSqlServer(config.GetConnectionString("Default"), sql => sql.EnableRetryOnFailure(3)));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ICrisisService, CrisisService>();
        services.AddScoped<IAuditQueryService, AuditQueryService>();
        return services;
    }
}

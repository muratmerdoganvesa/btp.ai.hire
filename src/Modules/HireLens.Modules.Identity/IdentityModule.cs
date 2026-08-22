using HireLens.Contracts.Identity;
using HireLens.Modules.Identity.Application;
using HireLens.Modules.Identity.Endpoints;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace HireLens.Modules.Identity;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        services.AddScoped<UserService>();
        services.AddScoped<IUserService>(sp => sp.GetRequiredService<UserService>());
        services.AddScoped<IUserCreatePort>(sp => sp.GetRequiredService<UserService>());
        return services;
    }

    public static IEndpointRouteBuilder MapIdentityModule(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapIdentityEndpoints();
        return endpoints;
    }
}

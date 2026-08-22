using HireLens.Contracts.Tenancy;
using HireLens.Modules.Tenancy.Application;
using HireLens.Modules.Tenancy.Endpoints;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace HireLens.Modules.Tenancy;

public static class TenancyModule
{
    public static IServiceCollection AddTenancyModule(this IServiceCollection services)
    {
        services.AddScoped<TenantService>();
        services.AddScoped<ITenantService>(sp => sp.GetRequiredService<TenantService>());
        services.AddScoped<ITenantProvisionPort>(sp => sp.GetRequiredService<TenantService>());
        return services;
    }

    public static IEndpointRouteBuilder MapTenancyModule(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapTenancyEndpoints();
        return endpoints;
    }
}

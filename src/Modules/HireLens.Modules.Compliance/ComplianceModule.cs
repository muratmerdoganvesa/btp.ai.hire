using HireLens.Modules.Compliance.Application;
using HireLens.Modules.Compliance.Endpoints;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace HireLens.Modules.Compliance;

public static class ComplianceModule
{
    public static IServiceCollection AddComplianceModule(this IServiceCollection services)
    {
        services.AddScoped<IComplianceService, ComplianceService>();
        return services;
    }

    public static IEndpointRouteBuilder MapComplianceModule(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapComplianceEndpoints();
        return endpoints;
    }
}

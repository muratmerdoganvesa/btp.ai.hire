using HireLens.Contracts.Recruiting;
using HireLens.Modules.Recruiting.Application;
using HireLens.Modules.Recruiting.Endpoints;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace HireLens.Modules.Recruiting;

public static class RecruitingModule
{
    public static IServiceCollection AddRecruitingModule(this IServiceCollection services)
    {
        services.AddScoped<PositionService>();
        services.AddScoped<IPositionService>(sp => sp.GetRequiredService<PositionService>());
        services.AddScoped<IPositionReadPort>(sp => sp.GetRequiredService<PositionService>());
        services.AddScoped<IPositionWritePort>(sp => sp.GetRequiredService<PositionService>());
        services.AddScoped<PublicApplicationService>();
        services.AddScoped<IPublicApplicationService>(sp => sp.GetRequiredService<PublicApplicationService>());
        return services;
    }

    public static IEndpointRouteBuilder MapRecruitingModule(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapRecruitingEndpoints();
        endpoints.MapPublicRecruitingEndpoints();
        return endpoints;
    }
}

using HireLens.Contracts.Matching;
using HireLens.Modules.Matching.Application;
using HireLens.Modules.Matching.Endpoints;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace HireLens.Modules.Matching;

public static class MatchingModule
{
    public static IServiceCollection AddMatchingModule(this IServiceCollection services)
    {
        services.AddScoped<MatchingJob>();
        services.AddScoped<IEvaluationService>(sp => sp.GetRequiredService<MatchingJob>());
        services.AddScoped<IEvaluationReadPort>(sp => sp.GetRequiredService<MatchingJob>());
        services.AddScoped<IEvaluationWritePort>(sp => sp.GetRequiredService<MatchingJob>());
        services.AddScoped<IEvaluationBlendPort>(sp => sp.GetRequiredService<MatchingJob>());
        return services;
    }

    public static IEndpointRouteBuilder MapMatchingModule(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMatchingEndpoints();
        return endpoints;
    }
}

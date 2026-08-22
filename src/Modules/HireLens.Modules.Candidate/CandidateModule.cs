using HireLens.Contracts.Candidates;
using HireLens.Modules.Candidate.Application;
using HireLens.Modules.Candidate.Endpoints;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace HireLens.Modules.Candidate;

public static class CandidateModule
{
    public static IServiceCollection AddCandidateModule(this IServiceCollection services)
    {
        services.AddScoped<CandidateService>();
        services.AddScoped<ICandidateService>(sp => sp.GetRequiredService<CandidateService>());
        services.AddScoped<ICandidateReadPort>(sp => sp.GetRequiredService<CandidateService>());
        services.AddScoped<ICandidateWritePort>(sp => sp.GetRequiredService<CandidateService>());
        return services;
    }

    public static IEndpointRouteBuilder MapCandidateModule(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapCandidateEndpoints();
        return endpoints;
    }
}

using HireLens.Modules.Interview.Application;
using HireLens.Modules.Interview.Endpoints;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace HireLens.Modules.Interview;

public static class InterviewModule
{
    public static IServiceCollection AddInterviewModule(this IServiceCollection services)
    {
        services.AddSingleton<InterviewTokenSigner>();
        services.AddScoped<IInterviewService, InterviewService>();
        services.AddScoped<IInterviewEvaluationService, InterviewEvaluationService>();
        return services;
    }

    public static IEndpointRouteBuilder MapInterviewModule(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapInterviewEndpoints();
        endpoints.MapInterviewEvaluationEndpoints();
        return endpoints;
    }
}

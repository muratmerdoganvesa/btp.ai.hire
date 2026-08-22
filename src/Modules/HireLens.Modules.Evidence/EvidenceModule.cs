using HireLens.Contracts.Evidence;
using HireLens.Modules.Evidence.Application;
using Microsoft.Extensions.DependencyInjection;

namespace HireLens.Modules.Evidence;

public static class EvidenceModule
{
    public static IServiceCollection AddEvidenceModule(this IServiceCollection services)
    {
        services.AddScoped<IEvidenceScoring, EvidenceScoring>();
        return services;
    }
}

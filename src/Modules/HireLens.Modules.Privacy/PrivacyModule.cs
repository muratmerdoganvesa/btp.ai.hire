using HireLens.Contracts.Privacy;
using HireLens.Modules.Privacy.Application;
using Microsoft.Extensions.DependencyInjection;

namespace HireLens.Modules.Privacy;

public static class PrivacyModule
{
    public static IServiceCollection AddPrivacyModule(this IServiceCollection services)
    {
        services.AddScoped<IPrivacyService, PrivacyService>();
        services.AddScoped<IPrivacyConsentPort>(sp => sp.GetRequiredService<IPrivacyService>());
        return services;
    }
}

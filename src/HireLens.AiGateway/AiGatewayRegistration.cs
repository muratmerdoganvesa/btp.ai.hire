using HireLens.AiGateway.Masking;
using HireLens.AiGateway.Providers;
using HireLens.AiGateway.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HireLens.AiGateway;

public static class AiGatewayRegistration
{
    public static IServiceCollection AddAiGateway(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AiGatewayOptions>(configuration.GetSection(AiGatewayOptions.SectionName));
        services.Configure<SapAiCoreOptions>(options =>
        {
            configuration.GetSection(SapAiCoreOptions.SectionName).Bind(options);
            options.ServiceKeyJson ??= configuration["AICORE_SERVICE_KEY"];
            options.DeploymentId ??= configuration["AICORE_DEPLOYMENT_ID"];
            var resourceGroup = configuration["AICORE_RESOURCE_GROUP"];
            if (!string.IsNullOrWhiteSpace(resourceGroup))
            {
                options.ResourceGroup = resourceGroup;
            }
        });

        services.AddSingleton<IPiiMasker, PiiMasker>();
        services.AddSingleton<ModelRouter>();
        services.AddHttpClient<SapOrchestrationProvider>();
        services.AddScoped<StubAiProvider>();
        services.AddScoped<IAiProvider>(sp =>
        {
            var key = configuration["AICORE_SERVICE_KEY"]
                ?? configuration[$"{SapAiCoreOptions.SectionName}:ServiceKeyJson"];
            return string.IsNullOrWhiteSpace(key)
                ? sp.GetRequiredService<StubAiProvider>()
                : sp.GetRequiredService<SapOrchestrationProvider>();
        });
        services.AddScoped<IAiGateway, AiGateway>();
        return services;
    }
}

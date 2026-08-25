using HireLens.AiGateway.Masking;
using HireLens.AiGateway.Prompts;
using HireLens.AiGateway.Providers;
using HireLens.AiGateway.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HireLens.AiGateway;

public static class AiGatewayRegistration
{
    public static IServiceCollection AddAiGateway(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AiGatewayOptions>(configuration.GetSection(AiGatewayOptions.SectionName));
        services.Configure<SapAiCoreOptions>(options =>
        {
            configuration.GetSection(SapAiCoreOptions.SectionName).Bind(options);
            configuration.GetSection("AiCore").Bind(options);

            options.ServiceKeyJson ??= configuration["AICORE_SERVICE_KEY"];
            options.DeploymentId ??= configuration["AICORE_DEPLOYMENT_ID"]
                ?? configuration["AiCore:DeploymentId"];
            options.ClientId ??= configuration["AiCore:ClientId"];
            options.ClientSecret ??= configuration["AiCore:ClientSecret"];
            options.AiApiUrl ??= configuration["AiCore:AiApiUrl"];
            options.XsuaaUrl ??= configuration["AiCore:XsuaaUrl"];

            var resourceGroup = configuration["AICORE_RESOURCE_GROUP"] ?? configuration["AiCore:ResourceGroup"];
            if (!string.IsNullOrWhiteSpace(resourceGroup))
            {
                options.ResourceGroup = resourceGroup;
            }

            var modelName = configuration["AiCore:ModelName"];
            if (!string.IsNullOrWhiteSpace(modelName))
            {
                options.ModelName = modelName;
            }

            var modelVersion = configuration["AiCore:ModelVersion"];
            if (!string.IsNullOrWhiteSpace(modelVersion))
            {
                options.ModelVersion = modelVersion;
            }
        });

        services.AddSingleton<IPiiMasker, PiiMasker>();
        services.AddSingleton<ModelRouter>();
        services.AddSingleton<IPromptRegistry, PromptRegistry>();
        services.AddSingleton<IJsonSchemaRegistry, JsonSchemaRegistry>();

        services.AddHttpClient("aicore");
        services.AddSingleton<AiCoreTokenProvider>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return new AiCoreTokenProvider(
                factory.CreateClient("aicore"),
                sp.GetRequiredService<IOptions<SapAiCoreOptions>>(),
                sp.GetRequiredService<ILogger<AiCoreTokenProvider>>());
        });

        services.AddHttpClient<OrchestrationClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(120);
        });

        services.AddScoped<SapOrchestrationProvider>();
        services.AddScoped<StubAiProvider>();
        services.AddScoped<IAiProvider>(sp =>
        {
            var key = configuration["AICORE_SERVICE_KEY"]
                ?? configuration[$"{SapAiCoreOptions.SectionName}:ServiceKeyJson"];
            var clientId = configuration["AiCore:ClientId"]
                ?? configuration[$"{SapAiCoreOptions.SectionName}:ClientId"];
            var configured = !string.IsNullOrWhiteSpace(key) || !string.IsNullOrWhiteSpace(clientId);
            return configured
                ? sp.GetRequiredService<SapOrchestrationProvider>()
                : sp.GetRequiredService<StubAiProvider>();
        });
        services.AddScoped<IAiGateway, AiGateway>();
        return services;
    }
}

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

            options.ServiceKeyJson ??= AiCoreServiceKey.Coalesce(
                configuration["AICORE_SERVICE_KEY"]
                ?? configuration[$"{SapAiCoreOptions.SectionName}:ServiceKeyJson"],
                TryReadServiceKeyFile(
                    configuration["SapAiCore:ServiceKeyPath"]
                    ?? configuration["AiCore:ServiceKeyPath"]
                    ?? "aicore-service-key.json"));

            options.DeploymentId ??= configuration["AICORE_DEPLOYMENT_ID"]
                ?? configuration["AiCore:DeploymentId"];
            options.CriteriaExtractionDeploymentId ??= configuration["AICORE_CRITERIA_DEPLOYMENT_ID"];
            options.CvExtractionDeploymentId ??= configuration["AICORE_CV_EXTRACTION_DEPLOYMENT_ID"];
            options.MatchingDeploymentId ??= configuration["AICORE_MATCHING_DEPLOYMENT_ID"];
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
            var env = sp.GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>();
            if (string.Equals(env.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase))
            {
                return sp.GetRequiredService<StubAiProvider>();
            }

            var opts = sp.GetRequiredService<IOptions<SapAiCoreOptions>>().Value;
            var key = opts.ServiceKeyJson
                ?? configuration["AICORE_SERVICE_KEY"]
                ?? configuration[$"{SapAiCoreOptions.SectionName}:ServiceKeyJson"];
            var clientId = opts.ClientId
                ?? configuration["AiCore:ClientId"]
                ?? configuration[$"{SapAiCoreOptions.SectionName}:ClientId"];
            var configured = AiCoreServiceKey.IsValidBindingJson(key)
                || !string.IsNullOrWhiteSpace(key)
                || !string.IsNullOrWhiteSpace(clientId);
            return configured
                ? sp.GetRequiredService<SapOrchestrationProvider>()
                : sp.GetRequiredService<StubAiProvider>();
        });
        services.AddScoped<IAiGateway, AiGateway>();
        return services;
    }

    /// <summary>
    /// Dev convenience: load AI Core binding JSON from a gitignored local file.
    /// Searches content directory, then walks up for hirelens/aicore-service-key.json.
    /// </summary>
    private static string? TryReadServiceKeyFile(string relativeOrAbsolute)
    {
        foreach (var candidate in EnumerateKeyPaths(relativeOrAbsolute))
        {
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateKeyPaths(string relativeOrAbsolute)
    {
        if (Path.IsPathRooted(relativeOrAbsolute))
        {
            yield return relativeOrAbsolute;
            yield break;
        }

        yield return Path.GetFullPath(relativeOrAbsolute);
        yield return Path.Combine(Directory.GetCurrentDirectory(), relativeOrAbsolute);
        yield return Path.Combine(Directory.GetCurrentDirectory(), "hirelens", relativeOrAbsolute);
        yield return Path.Combine(AppContext.BaseDirectory, relativeOrAbsolute);

        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            yield return Path.Combine(dir.FullName, relativeOrAbsolute);
            yield return Path.Combine(dir.FullName, "hirelens", relativeOrAbsolute);
            if (dir.Name.Equals("hirelens", StringComparison.OrdinalIgnoreCase))
            {
                yield return Path.Combine(dir.FullName, relativeOrAbsolute);
            }

            dir = dir.Parent;
        }
    }
}

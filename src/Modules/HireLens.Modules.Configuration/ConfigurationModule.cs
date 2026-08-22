using HireLens.Contracts.Configuration;
using HireLens.Infrastructure.Hosting;
using HireLens.Modules.Configuration.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace HireLens.Modules.Configuration;

public static class ConfigurationModule
{
    public static IServiceCollection AddConfigurationModule(this IServiceCollection services)
    {
        services.AddScoped<ConfigurationService>();
        services.AddScoped<IConfigurationService>(sp => sp.GetRequiredService<ConfigurationService>());
        services.AddScoped<IPromptCatalog>(sp => sp.GetRequiredService<ConfigurationService>());
        services.AddScoped<IThemeReader>(sp => sp.GetRequiredService<ConfigurationService>());
        services.AddScoped<IInterviewWeightPolicy>(sp => sp.GetRequiredService<ConfigurationService>());
        return services;
    }

    public static IEndpointRouteBuilder MapConfigurationModule(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/theme", async (IConfigurationService config, CancellationToken ct) =>
            HttpResults.From(await config.GetThemeAsync(ct))).WithTags("Configuration").RequireAuthorization();
        endpoints.MapPut("/api/theme", async (ThemeDto request, IConfigurationService config, CancellationToken ct) =>
            HttpResults.From(await config.UpdateThemeAsync(request, ct))).WithTags("Configuration").RequireAuthorization();

        endpoints.MapGet("/api/rubrics", async (IConfigurationService config, CancellationToken ct) =>
            HttpResults.From(await config.ListRubricsAsync(ct))).WithTags("Configuration").RequireAuthorization();
        endpoints.MapPost("/api/rubrics", async (UpsertRubricRequest request, IConfigurationService config, CancellationToken ct) =>
        {
            var result = await config.CreateRubricAsync(request, ct);
            return result.IsSuccess ? Results.Created($"/api/rubrics/{result.Value.Id}", result.Value) : HttpResults.From(result);
        }).WithTags("Configuration").RequireAuthorization();

        endpoints.MapGet("/api/model-policies", async (IConfigurationService config, CancellationToken ct) =>
            HttpResults.From(await config.ListPoliciesAsync(ct))).WithTags("Configuration").RequireAuthorization();
        endpoints.MapPut("/api/model-policies", async (UpsertModelPolicyRequest request, IConfigurationService config, CancellationToken ct) =>
            HttpResults.From(await config.UpsertPolicyAsync(request, ct))).WithTags("Configuration").RequireAuthorization();

        endpoints.MapGet("/api/prompt-overrides", async (IConfigurationService config, CancellationToken ct) =>
            HttpResults.From(await config.ListPromptsAsync(ct))).WithTags("Configuration").RequireAuthorization();
        endpoints.MapPut("/api/prompt-overrides", async (UpsertPromptOverrideRequest request, IConfigurationService config, CancellationToken ct) =>
            HttpResults.From(await config.UpsertPromptAsync(request, ct))).WithTags("Configuration").RequireAuthorization();

        endpoints.MapPost("/api/admin/tenants/provision", async (ProvisionTenantRequest request, IConfigurationService config, CancellationToken ct) =>
        {
            var result = await config.ProvisionAsync(request, ct);
            return result.IsSuccess ? Results.Created($"/api/tenants/{result.Value.TenantId}", result.Value) : HttpResults.From(result);
        }).WithTags("Configuration").RequireAuthorization();

        return endpoints;
    }
}

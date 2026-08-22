using HireLens.Contracts.Analytics;
using HireLens.Contracts.Documents;
using HireLens.Infrastructure.Hosting;
using HireLens.Modules.Analytics.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace HireLens.Modules.Analytics;

public static class AnalyticsModule
{
    public static IServiceCollection AddAnalyticsModule(this IServiceCollection services)
    {
        services.AddScoped<AnalyticsService>();
        services.AddScoped<IAnalyticsService>(sp => sp.GetRequiredService<AnalyticsService>());
        services.AddScoped<IPromptExperimentPort>(sp => sp.GetRequiredService<AnalyticsService>());
        services.AddScoped<IParseCache>(sp => sp.GetRequiredService<AnalyticsService>());
        return services;
    }

    public static IEndpointRouteBuilder MapAnalyticsModule(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/analytics/funnel", async (IAnalyticsService analytics, CancellationToken ct) =>
            HttpResults.From(await analytics.FunnelAsync(ct))).WithTags("Analytics").RequireAuthorization();
        endpoints.MapGet("/api/analytics/load", async (IAnalyticsService analytics, CancellationToken ct) =>
            HttpResults.From(await analytics.LoadAsync(ct))).WithTags("Analytics").RequireAuthorization();
        endpoints.MapGet("/api/analytics/sources", async (IAnalyticsService analytics, CancellationToken ct) =>
            HttpResults.From(await analytics.SourcesAsync(ct))).WithTags("Analytics").RequireAuthorization();
        endpoints.MapGet("/api/analytics/bias", async (IAnalyticsService analytics, CancellationToken ct) =>
            HttpResults.From(await analytics.BiasAsync(ct))).WithTags("Analytics").RequireAuthorization();
        endpoints.MapGet("/api/analytics/drift", async (IAnalyticsService analytics, CancellationToken ct) =>
            HttpResults.From(await analytics.DriftAsync(ct))).WithTags("Analytics").RequireAuthorization();
        endpoints.MapGet("/api/analytics/cost", async (IAnalyticsService analytics, CancellationToken ct) =>
            HttpResults.From(await analytics.CostAsync(ct))).WithTags("Analytics").RequireAuthorization();
        endpoints.MapPost("/api/analytics/benchmark", async (IAnalyticsService analytics, CancellationToken ct) =>
            HttpResults.From(await analytics.BenchmarkAsync(ct))).WithTags("Analytics").RequireAuthorization();
        endpoints.MapPost("/api/analytics/experiments", async (PromptExperimentDto request, IAnalyticsService analytics, CancellationToken ct) =>
            HttpResults.From(await analytics.OpenExperimentAsync(request, ct))).WithTags("Analytics").RequireAuthorization();
        return endpoints;
    }
}
